// Copyright (c) 2026 Oleksiy Slyshyk
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;

namespace Mono.IO.Ports
{
#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
    internal static partial class NativeWindows
    {
        // Windows API Constants
        internal const uint GenericRead = 0x80000000;
        internal const uint GenericWrite = 0x40000000;
        internal const uint OpenExisting = 3;
        internal const uint FileFlagOverlapped = 0x40000000;
        internal const uint PurgeRxClear = 0x0008;
        internal const uint PurgeTxClear = 0x0004;
        internal const uint WinInfiniteTimeout = 0xFFFFFFFF;
        internal const uint FileIOPending = 997;

        // Signal constants
        internal const uint SetRts = 3;
        internal const uint ClearRts = 4;
        internal const uint SetDtr = 5;
        internal const uint ClearDtr = 6;
        internal const uint SetBreak = 8;
        internal const uint ClearBreak = 9;
        internal const uint CtsOn = 0x0010;
        internal const uint DsrOn = 0x0020;
        internal const uint RsldOn = 0x0080;

        // Event constants
        internal const uint EvRxChar = 0x0001;
        internal const uint EvCts = 0x0008;
        internal const uint EvDsr = 0x0010;
        internal const uint EvRlsd = 0x0020;
        internal const uint EvBreak = 0x0040;
        internal const uint EvErr = 0x0080;
        internal const uint EvRing = 0x0100;

        // Registry constants
        internal static readonly IntPtr HKeyLocalMachine = new IntPtr(-2147483646);
        internal const int KeyEnumerateSubKeys = 8;
        internal const int KeyRead = 0x20019;
        internal const int RegSz = 1;

        // Native Windows API declarations
#if NET7_0_OR_GREATER
        [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
        internal static partial IntPtr CreateFile(string? port_name, uint desired_access,
                uint share_mode, IntPtr security_attrs, uint creation, uint flags,
                IntPtr template);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetupComm(IntPtr handle, int read_buffer_size, int write_buffer_size);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool PurgeComm(IntPtr handle, uint flags);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetCommTimeouts(IntPtr handle, ref Timeouts timeouts);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CloseHandle(IntPtr handle);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool ReadFile(IntPtr handle, byte* buffer, int bytes_to_read,
                    out int bytes_read, IntPtr overlapped);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetOverlappedResult(IntPtr handle, IntPtr overlapped,
                    ref int bytes_transferred, [MarshalAs(UnmanagedType.Bool)] bool wait);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool WriteFile(IntPtr handle, byte* buffer, int bytes_to_write,
                out int bytes_written, IntPtr overlapped);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetCommState(IntPtr handle, ref DCB dcb);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetCommState(IntPtr handle, ref DCB dcb);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ClearCommError(IntPtr handle, out uint errors, out CommStat stat);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetCommModemStatus(IntPtr handle, out uint flags);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool EscapeCommFunction(IntPtr handle, uint flags);
#else
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        internal static extern IntPtr CreateFile(string? port_name, uint desired_access,
                uint share_mode, IntPtr security_attrs, uint creation, uint flags,
                IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupComm(IntPtr handle, int read_buffer_size, int write_buffer_size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PurgeComm(IntPtr handle, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCommTimeouts(IntPtr handle, ref Timeouts timeouts);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern unsafe bool ReadFile(IntPtr handle, byte* buffer, int bytes_to_read,
                    out int bytes_read, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetOverlappedResult(IntPtr handle, IntPtr overlapped,
                    ref int bytes_transferred, [MarshalAs(UnmanagedType.Bool)] bool wait);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern unsafe bool WriteFile(IntPtr handle, byte* buffer, int bytes_to_write,
                out int bytes_written, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCommState(IntPtr handle, ref DCB dcb);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCommState(IntPtr handle, ref DCB dcb);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClearCommError(IntPtr handle, out uint errors, out CommStat stat);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCommModemStatus(IntPtr handle, out uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EscapeCommFunction(IntPtr handle, uint flags);
#endif

        // Native Windows registry API declarations
#if NET7_0_OR_GREATER
        [LibraryImport("advapi32.dll", EntryPoint = "RegOpenKeyExW", StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int RegOpenKeyEx(
            IntPtr hKey,
            string lpSubKey,
            int ulOptions,
            int samDesired,
            out IntPtr hkResult);
#else
        [DllImport("advapi32.dll", EntryPoint = "RegOpenKeyExW", CharSet = CharSet.Unicode, BestFitMapping = false)]
        internal static extern int RegOpenKeyEx(
            IntPtr hKey,
            string lpSubKey,
            int ulOptions,
            int samDesired,
            out IntPtr hkResult);
#endif

#if NET7_0_OR_GREATER
        [LibraryImport("advapi32.dll")]
        internal static partial int RegCloseKey(IntPtr hKey);
#else
        [DllImport("advapi32.dll")]
        internal static extern int RegCloseKey(IntPtr hKey);
#endif

#if NET7_0_OR_GREATER
        [LibraryImport("advapi32.dll", EntryPoint = "RegEnumValueW", StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int RegEnumValue(
            IntPtr hKey,
            int dwIndex,
            [Out] char[] lpValueName,
            ref int lpcbValueName,
            IntPtr lpReservedMustBeZero,
            IntPtr lpType,
            IntPtr lpData,
            IntPtr lpcbData);
#else
        [DllImport("advapi32.dll", EntryPoint = "RegEnumValueW", CharSet = CharSet.Unicode, BestFitMapping = false)]
        internal static extern int RegEnumValue(
            IntPtr hKey,
            int dwIndex,
            [Out] char[] lpValueName,
            ref int lpcbValueName,
            IntPtr lpReservedMustBeZero,
            IntPtr lpType,
            IntPtr lpData,
            IntPtr lpcbData);
#endif

#if NET7_0_OR_GREATER
        [LibraryImport("advapi32.dll", EntryPoint = "RegQueryValueExW", StringMarshalling = StringMarshalling.Utf16)]
        internal static partial int RegQueryValueEx(
            IntPtr hKey,
            string lpValueName,
            IntPtr lpReserved,
            ref int lpType,
            [Out] char[] lpData,
            ref int lpcbData);
#else
        [DllImport("advapi32.dll", EntryPoint = "RegQueryValueExW", CharSet = CharSet.Unicode, BestFitMapping = false)]
        internal static extern int RegQueryValueEx(
            IntPtr hKey,
            string lpValueName,
            IntPtr lpReserved,
            ref int lpType,
            [Out] char[] lpData,
            ref int lpcbData);
#endif

    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DCB
    {
        public int dcb_length;
        public int baud_rate;
        public int flags;
        public short w_reserved;
        public short xon_lim;
        public short xoff_lim;
        public byte byte_size;
        public byte parity;
        public byte stop_bits;
        public byte xon_char;
        public byte xoff_char;
        public byte error_char;
        public byte eof_char;
        public byte evt_char;
        public short w_reserved1;

        // flags:
        //const int fBinary = 0x0001;
        //const int fParity = 0x0002;
        const int fOutxCtsFlow = 0x0004;
        //const int fOutxDsrFlow1 = 0x0008;
        //const int fOutxDsrFlow2 = 0x0010;
        //const int fDtrControl = 0x00020;
        //const int fDsrSensitivity = 0x0040;
        //const int fTXContinueOnXoff = 0x0080;
        const int fOutX = 0x0100;
        const int fInX = 0x0200;
        //const int fErrorChar = 0x0400;
        //const int fNull = 0x0800;
        //const int fRtsControl1 = 0x1000;
        const int fRtsControl2 = 0x2000;
        //const int fAbortOnError = 0x4000;

        public void SetValues(int baud_rate, Parity parity, int byte_size, StopBits sb, Handshake hs)
        {
            switch (sb)
            {
                case StopBits.One:
                    stop_bits = 0;
                    break;
                case StopBits.OnePointFive:
                    stop_bits = 1;
                    break;
                case StopBits.Two:
                    stop_bits = 2;
                    break;
                default: // Shouldn't happen
                    break;
            }

            this.baud_rate = baud_rate;
            this.parity = (byte)parity;
            this.byte_size = (byte)byte_size;

            // Clear Handshake flags
            flags &= ~(fOutxCtsFlow | fOutX | fInX | fRtsControl2);

            // Set Handshake flags
            switch (hs)
            {
                case Handshake.None:
                    break;
                case Handshake.XOnXOff:
                    flags |= fOutX | fInX;
                    break;
                case Handshake.RequestToSend:
                    flags |= fOutxCtsFlow | fRtsControl2;
                    break;
                case Handshake.RequestToSendXOnXOff:
                    flags |= fOutxCtsFlow | fOutX | fInX | fRtsControl2;
                    break;
                default: // Shouldn't happen
                    break;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Timeouts
    {
        public uint ReadIntervalTimeout;
        public uint ReadTotalTimeoutMultiplier;
        public uint ReadTotalTimeoutConstant;
        public uint WriteTotalTimeoutMultiplier;
        public uint WriteTotalTimeoutConstant;

        public const uint MaxDWord = 0xFFFFFFFF;

        public Timeouts(int read_timeout, int write_timeout) : this()
        {
            SetValues(read_timeout, write_timeout);
        }

        public void SetValues(int read_timeout, int write_timeout)
        {
            // FIXME: The windows api docs are not very clear about read timeouts,
            // and we have to simulate infinite with a big value (uint.MaxValue - 1)
            ReadIntervalTimeout = MaxDWord;
            ReadTotalTimeoutMultiplier = MaxDWord;
            ReadTotalTimeoutConstant = (read_timeout == -1 ? MaxDWord - 1 : (uint)read_timeout);

            WriteTotalTimeoutMultiplier = 0;
            WriteTotalTimeoutConstant = (write_timeout == -1 ? MaxDWord : (uint)write_timeout);
        }

    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CommStat
    {
        public uint flags;
        public uint BytesIn;
        public uint BytesOut;
    }
}
