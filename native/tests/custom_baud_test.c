#include "serial.h"
#include <asm/termbits.h>
#include <asm/ioctls.h>
#include <sys/ioctl.h>
#include <stdio.h>
#include <stdlib.h>

void check_baud(int fd, unsigned int expected)
{
    struct termios2 settings;
    if (ioctl(fd, TCGETS2, &settings) != 0 ||
        settings.c_ispeed != expected || settings.c_ospeed != expected) {
        fputs("Requested baud rate was not applied\n", stderr);
        exit(1);
    }
}

void test_custom_baud(int fd)
{
    if (mdp_configure(fd, 123456, 0, 8, 1, 0) != 0) {
        fputs("Custom baud rate configuration failed\n", stderr);
        exit(1);
    }
    check_baud(fd, 123456);
}
