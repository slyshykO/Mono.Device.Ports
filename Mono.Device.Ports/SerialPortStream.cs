// Authors: Chris Toshok, Carlos Alberto Cortez
// (c) Copyright 2006 Novell, Inc.
// Copyright (c) 2026 Oleksiy Slyshyk
// SPDX-License-Identifier: MIT

using System;
using System.IO;

namespace Mono.IO.Ports
{
    internal class SerialPortStream : Stream, ISerialStream
    {
        private readonly SafeSerialHandle _handle;
        private int _readTimeout;
        private int _writeTimeout;

        public SerialPortStream(string portName, int baudRate, int dataBits, Parity parity, StopBits stopBits,
            bool dtrEnable, bool rtsEnable, Handshake handshake, int readTimeout, int writeTimeout,
            int readBufferSize, int writeBufferSize)
        {
            _handle = NativeSerial.Open(portName);
            if (_handle.IsInvalid) NativeSerial.ThrowIOException();
            try
            {
                ReadTimeout = readTimeout;
                WriteTimeout = writeTimeout;
                SetAttributes(baudRate, parity, dataBits, stopBits, handshake);
                SetSignal(SerialSignal.Dtr, dtrEnable);
                if (handshake != Handshake.RequestToSend && handshake != Handshake.RequestToSendXOnXOff)
                    SetSignal(SerialSignal.Rts, rtsEnable);
            }
            catch
            {
                _handle.Dispose();
                throw;
            }
        }

        public override bool CanRead => !_handle.IsClosed;
        public override bool CanWrite => !_handle.IsClosed;
        public override bool CanSeek => false;
        public override bool CanTimeout => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int ReadTimeout
        {
            get => _readTimeout;
            set
            {
                CheckDisposed();
                if (value < SerialPort.InfiniteTimeout) throw new ArgumentOutOfRangeException(nameof(value));
                _readTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get => _writeTimeout;
            set
            {
                CheckDisposed();
                if (value < SerialPort.InfiniteTimeout) throw new ArgumentOutOfRangeException(nameof(value));
                _writeTimeout = value;
            }
        }

        public override void Flush() => CheckDisposed();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            return Read(buffer.AsSpan(offset, count));
        }

        public override unsafe int Read(Span<byte> buffer)
        {
            CheckDisposed();
            if (buffer.IsEmpty) return 0;
            fixed (byte* pointer = buffer)
                return NativeSerial.CheckResult(NativeSerial.Read(_handle, pointer, buffer.Length, _readTimeout));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            Write(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        public override unsafe void Write(ReadOnlySpan<byte> buffer)
        {
            CheckDisposed();
            if (buffer.IsEmpty) return;
            fixed (byte* pointer = buffer)
                NativeSerial.CheckResult(NativeSerial.Write(_handle, pointer, buffer.Length, _writeTimeout));
        }

        protected override void Dispose(bool disposing)
        {
            _handle.Dispose();
            base.Dispose(disposing);
        }

        private void CheckDisposed()
        {
            if (_handle.IsClosed) throw new ObjectDisposedException(nameof(SerialPortStream));
        }

        public void SetAttributes(int baudRate, Parity parity, int dataBits, StopBits stopBits, Handshake handshake)
        {
            CheckDisposed();
            NativeSerial.CheckResult(NativeSerial.Configure(_handle, baudRate, parity, dataBits, stopBits, handshake));
        }

        public int BytesToRead
        {
            get { CheckDisposed(); return NativeSerial.CheckResult(NativeSerial.Available(_handle, 1)); }
        }

        public int BytesToWrite
        {
            get { CheckDisposed(); return NativeSerial.CheckResult(NativeSerial.Available(_handle, 0)); }
        }

        public void DiscardInBuffer()
        {
            CheckDisposed();
            NativeSerial.CheckResult(NativeSerial.Discard(_handle, 1));
        }

        public void DiscardOutBuffer()
        {
            CheckDisposed();
            NativeSerial.CheckResult(NativeSerial.Discard(_handle, 0));
        }

        public SerialSignal GetSignals()
        {
            CheckDisposed();
            return (SerialSignal)NativeSerial.CheckResult(NativeSerial.GetSignals(_handle));
        }

        public void SetSignal(SerialSignal signal, bool value)
        {
            CheckDisposed();
            NativeSerial.CheckResult(NativeSerial.SetSignal(_handle, signal, value ? 1 : 0));
        }

        public void SetBreakState(bool value)
        {
            CheckDisposed();
            NativeSerial.CheckResult(NativeSerial.SetBreak(_handle, value ? 1 : 0));
        }
    }
}
