#ifndef MDP_SERIAL_H
#define MDP_SERIAL_H

#include <stdint.h>

#if defined(__GNUC__)
#define MDP_EXPORT __attribute__((visibility("default")))
#else
#define MDP_EXPORT
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* ABI v1: fixed-width integers, pointer-sized handles, UTF-8 paths and byte buffers.
 * No platform structures/constants cross this boundary. -1 means errno is set;
 * read/write return -2 for a timeout. Timeout is milliseconds, -1 is infinite.
 * A failed write may already have transmitted a prefix of the buffer.
 */
#define MDP_TIMEOUT (-2)
enum mdp_signal { MDP_CD = 1, MDP_CTS = 2, MDP_DSR = 4, MDP_DTR = 8, MDP_RTS = 16 };

MDP_EXPORT intptr_t mdp_open(const char *path);
MDP_EXPORT int32_t mdp_close(intptr_t fd);
/* parity: none/odd/even/mark/space = 0..4; stop bits: one/two/1.5 = 1..3;
 * handshake: none/XON-XOFF/RTS-CTS/both = 0..3 (matching the managed enums). */
MDP_EXPORT int32_t mdp_configure(intptr_t fd, int32_t baud, int32_t parity,
                               int32_t data_bits, int32_t stop_bits, int32_t handshake);
MDP_EXPORT int32_t mdp_read(intptr_t fd, void *buffer, int32_t count, int32_t timeout);
MDP_EXPORT int32_t mdp_write(intptr_t fd, const void *buffer, int32_t count, int32_t timeout);
MDP_EXPORT int32_t mdp_available(intptr_t fd, int32_t input);
MDP_EXPORT int32_t mdp_discard(intptr_t fd, int32_t input);
MDP_EXPORT int32_t mdp_get_signals(intptr_t fd);
MDP_EXPORT int32_t mdp_set_signal(intptr_t fd, int32_t signal, int32_t value);
MDP_EXPORT int32_t mdp_set_break(intptr_t fd, int32_t value);
MDP_EXPORT const char *mdp_error_message(int32_t error);

#ifdef __cplusplus
}
#endif
#endif
