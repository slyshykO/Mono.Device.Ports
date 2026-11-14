// Copyright (c) 2026 Oleksiy Slyshyk
// SPDX-License-Identifier: MIT

#ifndef MDP_BAUD_RATES_H
#define MDP_BAUD_RATES_H

/* Included after the platform or kernel baud constants have been defined. */
static unsigned int mdp_baud_constant(int baud)
{
    switch (baud) {
#define RATE(n) case n: return B##n
        RATE(50); RATE(75); RATE(110); RATE(134); RATE(150); RATE(200);
        RATE(300); RATE(600); RATE(1200); RATE(1800); RATE(2400);
        RATE(4800); RATE(9600); RATE(19200); RATE(38400);
#ifdef B57600
        RATE(57600);
#endif
#ifdef B115200
        RATE(115200);
#endif
#ifdef B230400
        RATE(230400);
#endif
#ifdef B460800
        RATE(460800);
#endif
#ifdef B500000
        RATE(500000);
#endif
#ifdef B576000
        RATE(576000);
#endif
#ifdef B921600
        RATE(921600);
#endif
#ifdef B1000000
        RATE(1000000);
#endif
#ifdef B1152000
        RATE(1152000);
#endif
#ifdef B1500000
        RATE(1500000);
#endif
#ifdef B2000000
        RATE(2000000);
#endif
#ifdef B2500000
        RATE(2500000);
#endif
#ifdef B3000000
        RATE(3000000);
#endif
#ifdef B3500000
        RATE(3500000);
#endif
#ifdef B4000000
        RATE(4000000);
#endif
#undef RATE
        default: return B0;
    }
}

#endif
