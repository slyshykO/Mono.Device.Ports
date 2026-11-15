//
// System.IO.Ports.WinSerialStream.cs
//
// Authors:
//	Carlos Alberto Cortez (calberto.cortez@gmail.com)
//
// (c) Copyright 2006 Novell, Inc. (http://www.novell.com)
//

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.ComponentModel;

namespace Mono.IO.Ports
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    internal class WinSerialStream : Stream, ISerialStream, IDisposable
    {
        private readonly IntPtr _handle;
        private int _readTimeout;
        private int _writeTimeout;
        private bool _disposed;
        private readonly IntPtr _writeOverlapped;
        private readonly IntPtr _readOverlapped;
        private readonly ManualResetEvent _readEvent;
        private readonly ManualResetEvent _writeEvent;
        private Timeouts _timeouts;

        public WinSerialStream(string port_name, int baud_rate, int data_bits, Parity parity, StopBits sb,
                bool dtr_enable, bool rts_enable, Handshake hs, int read_timeout, int write_timeout,
                int read_buffer_size, int write_buffer_size)
        {
            _handle = NativeWindows.CreateFile(port_name != null && !port_name.StartsWith(@"\\.\")
                    ? @"\\.\" + port_name : port_name,
                    NativeWindows.GenericRead | NativeWindows.GenericWrite, 0, IntPtr.Zero, NativeWindows.OpenExisting,
                    NativeWindows.FileFlagOverlapped, IntPtr.Zero);

            if (_handle == new IntPtr(-1))
                ReportIOError(port_name);

            // Set port low level attributes
            SetAttributes(baud_rate, parity, data_bits, sb, hs);

            // Clean buffers and set sizes
            if (!NativeWindows.PurgeComm(_handle, NativeWindows.PurgeRxClear | NativeWindows.PurgeTxClear) ||
                    !NativeWindows.SetupComm(_handle, read_buffer_size, write_buffer_size))
                ReportIOError(null);

            // Set timeouts
            _readTimeout = read_timeout;
            _writeTimeout = write_timeout;
            _timeouts = new Timeouts(read_timeout, write_timeout);
            if (!NativeWindows.SetCommTimeouts(_handle, ref _timeouts))
                ReportIOError(null);

            /// Set DTR and RTS
            SetSignal(SerialSignal.Dtr, dtr_enable);

            if (hs != Handshake.RequestToSend &&
                    hs != Handshake.RequestToSendXOnXOff)
                SetSignal(SerialSignal.Rts, rts_enable);

            // Init overlapped structures
            NativeOverlapped wo = new NativeOverlapped();
            _writeEvent = new ManualResetEvent(false);
#pragma warning disable 618
            wo.EventHandle = _writeEvent.Handle;
            _writeOverlapped = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeOverlapped)));
            Marshal.StructureToPtr(wo, _writeOverlapped, true);

            NativeOverlapped ro = new NativeOverlapped();
            _readEvent = new ManualResetEvent(false);
            ro.EventHandle = _readEvent.Handle;
            _readOverlapped = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NativeOverlapped)));
            Marshal.StructureToPtr(ro, _readOverlapped, true);
#pragma warning restore 618
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanTimeout => true;
        public override bool CanWrite => true;

        public override int ReadTimeout
        {
            get => _readTimeout;
            set
            {
                if (value < 0 && value != SerialPort.InfiniteTimeout)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _timeouts.SetValues(value, _writeTimeout);
                if (!NativeWindows.SetCommTimeouts(_handle, ref _timeouts))
                    ReportIOError(null);

                _readTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get => _writeTimeout;
            set
            {
                if (value < 0 && value != SerialPort.InfiniteTimeout)
                    throw new ArgumentOutOfRangeException(nameof(value));

                _timeouts.SetValues(_readTimeout, value);
                if (!NativeWindows.SetCommTimeouts(_handle, ref _timeouts))
                    ReportIOError(null);

                _writeTimeout = value;
            }
        }

        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;
            NativeWindows.CloseHandle(_handle);
            Marshal.FreeHGlobal(_writeOverlapped);
            Marshal.FreeHGlobal(_readOverlapped);
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override void Close()
        {
            ((IDisposable)this).Dispose();
        }

        ~WinSerialStream()
        {
            Dispose(false);
        }

        public override void Flush()
        {
            CheckDisposed();
            // No do thing by now
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(Span<byte> buffer)
        {
            CheckDisposed();

            if (buffer.IsEmpty)
                return 0;

            int bytes_read;

            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    if (NativeWindows.ReadFile(_handle, ptr, buffer.Length, out bytes_read, _readOverlapped))
                        return bytes_read;

                    // Test for overlapped behavior
                    if (Marshal.GetLastWin32Error() != NativeWindows.FileIOPending)
                        ReportIOError(null);

                    if (!NativeWindows.GetOverlappedResult(_handle, _readOverlapped, ref bytes_read, true))
                        ReportIOError(null);

                }
            }

            if (bytes_read == 0)
                throw new TimeoutException(); // We didn't get any byte

            return bytes_read;
        }

        public override int Read([In, Out] byte[] buffer, int offset, int count)
        {
            return this.Read(buffer.AsSpan(offset, count));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.Write(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            CheckDisposed();

            if (buffer.IsEmpty)
                return;

            int bytes_written = 0;

            unsafe
            {
                fixed (byte* ptr = buffer)
                {
                    if (NativeWindows.WriteFile(_handle, ptr, buffer.Length, out bytes_written, _writeOverlapped))
                        return;

                    if (Marshal.GetLastWin32Error() != NativeWindows.FileIOPending)
                        ReportIOError(null);

                    if (!NativeWindows.GetOverlappedResult(_handle, _writeOverlapped, ref bytes_written, true))
                        ReportIOError(null);
                }
            }

            // If the operation timed out, then
            // we transferred less bytes than the requested ones
            if (bytes_written < buffer.Length)
                throw new TimeoutException();
        }


        public void SetAttributes(int baudRate, Parity parity, int dataBits, StopBits bits, Handshake hs)
        {
            DCB dcb = new DCB { dcb_length = Marshal.SizeOf<DCB>() };

            if (!NativeWindows.GetCommState(_handle, ref dcb))
                ReportIOError(null);

            dcb.SetValues(baudRate, parity, dataBits, bits, hs);

            if (!NativeWindows.SetCommState(_handle, ref dcb))
                ReportIOError(null);
        }

        void ReportIOError(string? optional_arg)
        {
            int error = Marshal.GetLastWin32Error();
            string message;
            switch (error)
            {
                case 2:
                case 3:
                    message = "The port `" + optional_arg + "' does not exist.";
                    break;
                case 87:
                    message = "Parameter is incorrect.";
                    break;
                default:
                    // As fallback, we show the win32 error
                    message = new Win32Exception().Message;
                    break;
            }

            throw new IOException(message);
        }

        void CheckDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        // ISerialStream members
        public void DiscardInBuffer()
        {
            if (!NativeWindows.PurgeComm(_handle, NativeWindows.PurgeRxClear))
                ReportIOError(null);
        }

        public void DiscardOutBuffer()
        {
            if (!NativeWindows.PurgeComm(_handle, NativeWindows.PurgeTxClear))
                ReportIOError(null);
        }

        public int BytesToRead
        {
            get
            {
                uint errors;
                CommStat stat;
                if (!NativeWindows.ClearCommError(_handle, out errors, out stat))
                    ReportIOError(null);

                return (int)stat.BytesIn;
            }
        }

        public int BytesToWrite
        {
            get
            {
                uint errors;
                CommStat stat;
                if (!NativeWindows.ClearCommError(_handle, out errors, out stat))
                    ReportIOError(null);

                return (int)stat.BytesOut;
            }
        }

        public SerialSignal GetSignals()
        {
            uint flags;
            if (!NativeWindows.GetCommModemStatus(_handle, out flags))
                ReportIOError(null);

            SerialSignal signals = SerialSignal.None;
            if ((flags & NativeWindows.RsldOn) != 0)
                signals |= SerialSignal.Cd;
            if ((flags & NativeWindows.CtsOn) != 0)
                signals |= SerialSignal.Cts;
            if ((flags & NativeWindows.DsrOn) != 0)
                signals |= SerialSignal.Dsr;

            return signals;
        }

        public void SetSignal(SerialSignal signal, bool value)
        {
            if (signal != SerialSignal.Rts && signal != SerialSignal.Dtr)
                throw new Exception("Wrong internal value");

            uint flag;
            if (signal == SerialSignal.Rts)
                if (value)
                    flag = NativeWindows.SetRts;
                else
                    flag = NativeWindows.ClearRts;
            else
                if (value)
                    flag = NativeWindows.SetDtr;
                else
                    flag = NativeWindows.ClearDtr;

            if (!NativeWindows.EscapeCommFunction(_handle, flag))
                ReportIOError(null);
        }

        public void SetBreakState(bool value)
        {
            if (!NativeWindows.EscapeCommFunction(_handle, value ? NativeWindows.SetBreak : NativeWindows.ClearBreak))
                ReportIOError(null);
        }

    }
}
