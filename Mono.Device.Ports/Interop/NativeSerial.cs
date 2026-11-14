using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Mono.IO.Ports
{
    internal sealed class SafeSerialHandle : SafeHandleMinusOneIsInvalid
    {
        public SafeSerialHandle() : base(true) { }
        protected override bool ReleaseHandle() => NativeSerial.Close(handle) == 0;
    }

    internal static class NativeSerial
    {
        private const string Library = "MonoDevicePortsNative";

        [DllImport(Library, EntryPoint = "mdp_open", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern SafeSerialHandle Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        [DllImport(Library, EntryPoint = "mdp_close", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern int Close(IntPtr fd);

        [DllImport(Library, EntryPoint = "mdp_configure", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern int Configure(SafeSerialHandle handle, int baudRate, Parity parity,
            int dataBits, StopBits stopBits, Handshake handshake);

        [DllImport(Library, EntryPoint = "mdp_read", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern unsafe int Read(SafeSerialHandle handle, byte* buffer, int count, int timeout);

        [DllImport(Library, EntryPoint = "mdp_write", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern unsafe int Write(SafeSerialHandle handle, byte* buffer, int count, int timeout);

        [DllImport(Library, EntryPoint = "mdp_available", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern int Available(SafeSerialHandle handle, int input);

        [DllImport(Library, EntryPoint = "mdp_discard", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern int Discard(SafeSerialHandle handle, int input);

        [DllImport(Library, EntryPoint = "mdp_get_signals", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern int GetSignals(SafeSerialHandle handle);

        [DllImport(Library, EntryPoint = "mdp_set_signal", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern int SetSignal(SafeSerialHandle handle, SerialSignal signal, int value);

        [DllImport(Library, EntryPoint = "mdp_set_break", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        internal static extern int SetBreak(SafeSerialHandle handle, int value);

        [DllImport(Library, EntryPoint = "mdp_error_message", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr ErrorMessage(int error);

        internal static int CheckResult(int result)
        {
            if (result == -2) throw new TimeoutException("The serial operation timed out.");
            if (result < 0) ThrowIOException();
            return result;
        }

        internal static void ThrowIOException()
        {
            int error = Marshal.GetLastWin32Error();
            string message = Marshal.PtrToStringAnsi(ErrorMessage(error)) ?? "Serial I/O failed";
            throw new IOException($"{message} (errno {error}).");
        }
    }
}
