/* Keep kernel termios definitions separate from libc's <termios.h>. */
#include <asm/termbits.h>
#include <asm/ioctls.h>
#include <sys/ioctl.h>
#include "baud_rates.h"

int mdp_set_baud(int fd, int baud)
{
    struct termios2 settings;
    if (ioctl(fd, TCGETS2, &settings) == -1) return -1;
    settings.c_cflag &= ~(CBAUD | (CBAUD << IBSHIFT));
    unsigned int speed = mdp_baud_constant(baud);
    if (speed == B0) speed = BOTHER;
    settings.c_cflag |= speed | (speed << IBSHIFT);
    settings.c_ispeed = (unsigned int)baud;
    settings.c_ospeed = (unsigned int)baud;
    return ioctl(fd, TCSETS2, &settings);
}
