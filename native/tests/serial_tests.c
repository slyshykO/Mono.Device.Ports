#include "serial.h"
#include <errno.h>
#include <fcntl.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/time.h>
#include <termios.h>
#include <time.h>
#include <unistd.h>

#define CHECK(expr) do { if (!(expr)) { fprintf(stderr, "%s:%d: %s (errno %d)\n", __FILE__, __LINE__, #expr, errno); exit(1); } } while (0)
#ifdef MDP_HAVE_TERMIOS2
void test_custom_baud(int fd);
void check_baud(int fd, unsigned int expected);
#endif
static void interrupt_poll(int signal) { (void)signal; }
static long long milliseconds(void)
{
    struct timespec t;
    CHECK(clock_gettime(CLOCK_MONOTONIC, &t) == 0);
    return (long long)t.tv_sec * 1000 + t.tv_nsec / 1000000;
}

int main(void)
{
    CHECK(mdp_open("/dev/mono-device-ports-nonexistent") == -1 && errno == ENOENT);
    int master = posix_openpt(O_RDWR | O_NOCTTY);
    CHECK(master >= 0);
    CHECK(grantpt(master) == 0 && unlockpt(master) == 0);
    int fd = (int)mdp_open(ptsname(master));
    CHECK(fd >= 0);
    CHECK((fcntl(fd, F_GETFD) & FD_CLOEXEC) != 0);
    CHECK(mdp_configure(fd, 115200, 0, 8, 1, 0) == 0);
    struct termios attributes;
    CHECK(tcgetattr(fd, &attributes) == 0);
#ifdef MDP_HAVE_TERMIOS2
    check_baud(fd, 115200);
#else
    CHECK(cfgetospeed(&attributes) == B115200);
#endif
    CHECK(!(attributes.c_lflag & (ICANON | ECHO)));
    CHECK(mdp_configure(fd, 0, 0, 8, 1, 0) == -1 && errno == EINVAL);
#ifdef B1000000
    CHECK(mdp_configure(fd, 1000000, 0, 8, 1, 0) == 0);
#ifdef MDP_HAVE_TERMIOS2
    check_baud(fd, 1000000);
#else
    CHECK(tcgetattr(fd, &attributes) == 0 && cfgetospeed(&attributes) == B1000000);
#endif
#endif
#ifdef MDP_HAVE_TERMIOS2
    test_custom_baud(fd);
#endif
    CHECK(mdp_configure(fd, 115200, 0, 8, 1, 0) == 0);
    const unsigned char data[] = {0, 1, 0x11, 0x13, 0x80, 0xff};
    unsigned char buffer[sizeof(data)];
    CHECK(write(master, data, sizeof(data)) == sizeof(data));
    int received = 0;
    while (received < (int)sizeof(data)) {
        int n = mdp_read(fd, buffer + received, sizeof(data) - received, 1000);
        CHECK(n > 0);
        received += n;
    }
    CHECK(memcmp(buffer, data, sizeof(data)) == 0);
    CHECK(mdp_write(fd, data, sizeof(data), 1000) == 0);
    CHECK(read(master, buffer, sizeof(buffer)) == sizeof(buffer));
    CHECK(memcmp(buffer, data, sizeof(data)) == 0);
    CHECK(mdp_read(fd, NULL, 0, 0) == 0);
    CHECK(mdp_read(fd, buffer, sizeof(buffer), 0) == MDP_TIMEOUT);
    CHECK(mdp_read(-1, buffer, sizeof(buffer), 0) == -1 && errno == EBADF);
    CHECK(mdp_write(-1, data, sizeof(data), 0) == -1 && errno == EBADF);

    struct sigaction action = {0};
    action.sa_handler = interrupt_poll;
    sigemptyset(&action.sa_mask);
    CHECK(sigaction(SIGALRM, &action, NULL) == 0);
    struct itimerval timer = {{0, 10000}, {0, 10000}};
    CHECK(setitimer(ITIMER_REAL, &timer, NULL) == 0);
    long long start = milliseconds();
    CHECK(mdp_read(fd, buffer, sizeof(buffer), 80) == MDP_TIMEOUT);
    long long duration = milliseconds() - start;
    struct itimerval stopped = {{0, 0}, {0, 0}};
    CHECK(setitimer(ITIMER_REAL, &stopped, NULL) == 0);
    CHECK(duration >= 60 && duration < 1000);

    unsigned char *large = calloc(1024 * 1024, 1);
    CHECK(large != NULL);
    start = milliseconds();
    CHECK(mdp_write(fd, large, 1024 * 1024, 80) == MDP_TIMEOUT);
    CHECK(milliseconds() - start < 1000);
    free(large);
    CHECK(mdp_discard(fd, 0) == 0);
    CHECK(close(master) == 0);
    CHECK(mdp_read(fd, buffer, sizeof(buffer), 100) == -1);
    CHECK(mdp_close(fd) == 0);
    puts("Native serial tests passed: baud rates, binary I/O, timeout deadlines, EINTR, partial writes, disconnect.");
    return 0;
}
