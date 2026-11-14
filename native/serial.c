#include "serial.h"

#include <errno.h>
#include <fcntl.h>
#include <poll.h>
#include <stddef.h>
#include <string.h>
#include <sys/ioctl.h>
#include <termios.h>
#include <time.h>
#include <unistd.h>
#ifdef __APPLE__
#include <IOKit/serial/ioss.h>
#endif
#ifdef MDP_HAVE_TERMIOS2
int mdp_set_baud(int fd, int baud);
#else
#include "baud_rates.h"
#if defined(__linux__) && defined(__GLIBC__) && B9600 == 9600
#error "These glibc headers require Linux termios2 support; install target kernel headers."
#endif
#endif

intptr_t mdp_open(const char *path)
{
    if (path == NULL) { errno = EINVAL; return -1; }
    int fd;
    do { fd = open(path, O_RDWR | O_NOCTTY | O_NONBLOCK | O_CLOEXEC); }
    while (fd == -1 && errno == EINTR);
    return fd;
}

int32_t mdp_close(intptr_t fd)
{
    /* Do not retry close: Linux releases the descriptor even on EINTR. */
    int result = close(fd);
    return result == -1 && errno == EINTR ? 0 : result;
}

int32_t mdp_configure(intptr_t fd, int32_t baud, int32_t parity,
                      int32_t data_bits, int32_t stop_bits, int32_t handshake)
{
    if (baud <= 0 || parity < 0 || parity > 4 || data_bits < 5 || data_bits > 8 ||
        stop_bits < 1 || stop_bits > 3 || handshake < 0 || handshake > 3 ||
        (stop_bits == 3 && data_bits != 5)) {
        errno = EINVAL;
        return -1;
    }
#ifndef CMSPAR
    if (parity >= 3) { errno = ENOTSUP; return -1; }
#endif
#ifndef MDP_HAVE_TERMIOS2
    speed_t speed = mdp_baud_constant(baud);
#ifndef __APPLE__
    if (speed == B0) { errno = ENOTSUP; return -1; }
#endif
#endif
    struct termios settings;
    if (tcgetattr(fd, &settings) == -1) return -1;
    cfmakeraw(&settings);
    settings.c_cflag |= CLOCAL | CREAD;
    settings.c_cflag &= ~(CSIZE | CSTOPB | PARENB | PARODD | CRTSCTS);
#ifdef CMSPAR
    settings.c_cflag &= ~CMSPAR;
#endif
    switch (data_bits) {
        case 5: settings.c_cflag |= CS5; break;
        case 6: settings.c_cflag |= CS6; break;
        case 7: settings.c_cflag |= CS7; break;
        case 8: settings.c_cflag |= CS8; break;
    }
    if (stop_bits != 1) settings.c_cflag |= CSTOPB;
    if (parity != 0) settings.c_cflag |= PARENB;
    if (parity == 1 || parity == 3) settings.c_cflag |= PARODD;
#ifdef CMSPAR
    if (parity >= 3) settings.c_cflag |= CMSPAR;
#endif
    settings.c_iflag &= ~(IXON | IXOFF | IXANY | INPCK | ISTRIP);
    settings.c_iflag |= IGNBRK;
    if (handshake == 1 || handshake == 3) settings.c_iflag |= IXON | IXOFF;
    if (handshake == 2 || handshake == 3) settings.c_cflag |= CRTSCTS;
    settings.c_cc[VSTART] = 0x11;
    settings.c_cc[VSTOP] = 0x13;
    settings.c_cc[VMIN] = 1;
    settings.c_cc[VTIME] = 0;
    /* Use kernel baud constants with termios2. New glibc headers bundled with
     * Zig may define numeric Bnnn values while older target libc symbols expect
     * encoded values. Keeping speed changes here avoids that ABI mismatch. */
#ifdef MDP_HAVE_TERMIOS2
    if (tcsetattr(fd, TCSANOW, &settings) == -1) return -1;
    return mdp_set_baud(fd, baud);
#else
    /* A custom baud is applied below; never silently accept this placeholder. */
    speed_t initial_speed = speed == B0 ? B38400 : speed;
    if (cfsetispeed(&settings, initial_speed) == -1 ||
        cfsetospeed(&settings, initial_speed) == -1 ||
        tcsetattr(fd, TCSANOW, &settings) == -1) return -1;
    if (speed == B0) {
#ifdef __APPLE__
        speed_t custom_speed = (speed_t)baud;
        return ioctl(fd, IOSSIOSPEED, &custom_speed);
#endif
    }
    return 0;
#endif
}

static int64_t now_ms(void)
{
    struct timespec ts;
    if (clock_gettime(CLOCK_MONOTONIC, &ts) == -1) return -1;
    return (int64_t)ts.tv_sec * 1000 + ts.tv_nsec / 1000000;
}

/* One absolute deadline is shared by all polls, interruptions and partial writes. */
static int wait_ready(int fd, short events, int timeout, int64_t deadline)
{
    for (;;) {
        int remaining = -1;
        if (timeout >= 0) {
            int64_t now = now_ms();
            if (now == -1) return -1;
            int64_t left = deadline - now;
            remaining = left > 0 ? (int)left : 0;
        }
        struct pollfd info = { fd, events, 0 };
        int result = poll(&info, 1, remaining);
        if (result == -1) {
            if (errno != EINTR) return -1;
            if (timeout >= 0 && now_ms() >= deadline) return MDP_TIMEOUT;
            continue;
        }
        if (result == 0) return MDP_TIMEOUT;
        if (info.revents & POLLNVAL) { errno = EBADF; return -1; }
        /* Drain readable bytes before reporting a disconnected peer. */
        if (info.revents & events) return 0;
        if (info.revents & (POLLERR | POLLHUP)) { errno = EIO; return -1; }
    }
}

static int validate_io(const void *buffer, int count, int timeout, int64_t *deadline)
{
    if (count < 0 || timeout < -1 || (buffer == NULL && count != 0)) {
        errno = EINVAL;
        return -1;
    }
    *deadline = 0;
    if (timeout >= 0) {
        int64_t now = now_ms();
        if (now == -1) return -1;
        *deadline = now + timeout;
    }
    return 0;
}

int32_t mdp_read(intptr_t fd, void *buffer, int32_t count, int32_t timeout)
{
    int64_t deadline;
    if (validate_io(buffer, count, timeout, &deadline) == -1) return -1;
    if (count == 0) return 0;
    if (fd < 0) { errno = EBADF; return -1; }
    for (;;) {
        int ready = wait_ready(fd, POLLIN, timeout, deadline);
        if (ready != 0) return ready;
        ssize_t result = read(fd, buffer, (size_t)count);
        if (result > 0) return (int32_t)result;
        if (result == 0) { errno = EIO; return -1; }
        if (errno != EINTR && errno != EAGAIN && errno != EWOULDBLOCK) return -1;
        if (timeout >= 0 && now_ms() >= deadline) return MDP_TIMEOUT;
    }
}

int32_t mdp_write(intptr_t fd, const void *buffer, int32_t count, int32_t timeout)
{
    int64_t deadline;
    if (validate_io(buffer, count, timeout, &deadline) == -1) return -1;
    if (count == 0) return 0;
    if (fd < 0) { errno = EBADF; return -1; }
    const unsigned char *next = buffer;
    while (count > 0) {
        int ready = wait_ready(fd, POLLOUT, timeout, deadline);
        if (ready != 0) return ready;
        ssize_t result = write(fd, next, (size_t)count);
        if (result > 0) {
            next += result;
            count -= (int32_t)result;
            if (count == 0) return 0;
        } else if (result == 0) {
            errno = EIO;
            return -1;
        } else if (errno != EINTR && errno != EAGAIN && errno != EWOULDBLOCK) {
            return -1;
        }
        if (timeout >= 0 && now_ms() >= deadline) return MDP_TIMEOUT;
    }
    return 0;
}

int32_t mdp_available(intptr_t fd, int32_t input)
{
    int count;
    return ioctl(fd, input ? FIONREAD : TIOCOUTQ, &count) == -1 ? -1 : count;
}

int32_t mdp_discard(intptr_t fd, int32_t input)
{
    return tcflush(fd, input ? TCIFLUSH : TCOFLUSH);
}

int32_t mdp_get_signals(intptr_t fd)
{
    int status;
    if (ioctl(fd, TIOCMGET, &status) == -1) return -1;
    return ((status & TIOCM_CAR) ? MDP_CD : 0) |
           ((status & TIOCM_CTS) ? MDP_CTS : 0) |
           ((status & TIOCM_DSR) ? MDP_DSR : 0) |
           ((status & TIOCM_DTR) ? MDP_DTR : 0) |
           ((status & TIOCM_RTS) ? MDP_RTS : 0);
}

int32_t mdp_set_signal(intptr_t fd, int32_t signal, int32_t value)
{
    if (signal != MDP_DTR && signal != MDP_RTS) { errno = EINVAL; return -1; }
    int bit = signal == MDP_DTR ? TIOCM_DTR : TIOCM_RTS;
    if (ioctl(fd, value ? TIOCMBIS : TIOCMBIC, &bit) == 0) return 0;
    /* Preserve the existing pseudo-terminal support. */
    return errno == EINVAL || errno == ENOTTY ? 0 : -1;
}

int32_t mdp_set_break(intptr_t fd, int32_t value)
{
    return ioctl(fd, value ? TIOCSBRK : TIOCCBRK);
}

const char *mdp_error_message(int32_t error)
{
    return strerror(error);
}
