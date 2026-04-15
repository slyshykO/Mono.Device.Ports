//
//
// This class has several problems:
//
//   * No buffering, the specification requires that there is buffering, this
//     matters because a few methods expose strings and chars and the reading
//     is encoding sensitive.   This means that when we do a read of a byte
//     sequence that can not be turned into a full string by the current encoding
//     we should keep a buffer with this data, and read from it on the next
//     iteration.
//
//   * Calls to read_serial from the unmanaged C do not check for errors,
//     like EINTR, that should be retried
//
//   * Calls to the encoder that do not consume all bytes because of partial
//     reads
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Linq;

namespace Mono.IO.Ports
{
    public enum Parity
    {
        None,
        Odd,
        Even,
        Mark,
        Space
    }

    public enum StopBits
    {
        None,
        One,
        Two,
        OnePointFive
    }

    public enum Handshake
    {
        None,
        XOnXOff,
        RequestToSend,
        RequestToSendXOnXOff
    }

    internal enum SerialSignal
    {
        None = 0,
        Cd = 1, // Carrier detect
        Cts = 2, // Clear to send
        Dsr = 4, // Data set ready
        Dtr = 8, // Data terminal ready
        Rts = 16 // Request to send
    }

    [MonitoringDescription("")]
    public class SerialPort : Component
    {
        public const int InfiniteTimeout = -1;
        private const int DefaultReadBufferSize = 4096;
        private const int DefaultWriteBufferSize = 2048;
        private const int DefaultBaudRate = 9600;
        private const int DefaultDataBits = 8;
        private const Parity DefaultParity = Parity.None;
        private const StopBits DefaultStopBits = StopBits.One;

        private int _baudRate;
        private Parity _parity;
        private StopBits _stopBits;
        private Handshake _handshake;
        private int _dataBits;
        private bool _breakState = false;
        private bool _dtrEnable = false;
        private bool _rtsEnable = false;
        private ISerialStream? _stream;
        private Encoding _encoding = Encoding.ASCII;
        private string _newLine = Environment.NewLine;
        private string _portName;
        private int _readTimeout = InfiniteTimeout;
        private int _writeTimeout = InfiniteTimeout;
        private int _readBufferSize = DefaultReadBufferSize;
        private int _writeBufferSize = DefaultWriteBufferSize;
        private readonly object _errorReceived = new object();
        private readonly object _dataReceived = new object();
        private readonly object _pinChanged = new object();

        public SerialPort() :
            this(GetDefaultPortName(), DefaultBaudRate, DefaultParity, DefaultDataBits, DefaultStopBits)
        {
        }

        public SerialPort(IContainer container) : this()
        {
            // TODO: What to do here?
        }

        public SerialPort(string portName) :
            this(portName, DefaultBaudRate, DefaultParity, DefaultDataBits, DefaultStopBits)
        {
        }

        public SerialPort(string portName, int baudRate) :
            this(portName, baudRate, DefaultParity, DefaultDataBits, DefaultStopBits)
        {
        }

        public SerialPort(string portName, int baudRate, Parity parity) :
            this(portName, baudRate, parity, DefaultDataBits, DefaultStopBits)
        {
        }

        public SerialPort(string portName, int baudRate, Parity parity, int dataBits) :
            this(portName, baudRate, parity, dataBits, DefaultStopBits)
        {
        }

        public SerialPort(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            _portName = portName;
            _baudRate = baudRate;
            _dataBits = dataBits;
            _stopBits = stopBits;
            this._parity = parity;
        }

        private static string GetDefaultPortName()
        {
            var ports = GetPortNames();
            if (ports.Length > 0)
            {
                return ports[0];
            }
            else
            {
                if (IsLinux)
                    return "ttyS0"; // Default for Unix
                else if (IsWindows)
                    return "COM1"; // Default for Windows
                else
                    return "MEEEH";
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public Stream? BaseStream
        {
            get
            {
                CheckOpen();
                return _stream as Stream;
            }
        }

        [DefaultValueAttribute(DefaultBaudRate)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int BaudRate
        {
            get => _baudRate;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (IsOpen)
                    _stream?.SetAttributes(value, _parity, _dataBits, _stopBits, _handshake);

                _baudRate = value;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public bool BreakState
        {
            get => _breakState;
            set
            {
                CheckOpen();
                if (value == _breakState)
                    return; // Do nothing.

                _stream?.SetBreakState(value);
                _breakState = value;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public int BytesToRead
        {
            get
            {
                CheckOpen();
                return _stream?.BytesToRead ?? -1;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public int BytesToWrite
        {
            get
            {
                CheckOpen();
                return _stream?.BytesToWrite ?? -1;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public bool CDHolding
        {
            get
            {
                CheckOpen();
                return (_stream?.GetSignals() & SerialSignal.Cd) != 0;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public bool CtsHolding
        {
            get
            {
                CheckOpen();
                return (_stream?.GetSignals() & SerialSignal.Cts) != 0;
            }
        }

        [DefaultValueAttribute(DefaultDataBits)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int DataBits
        {
            get => _dataBits;
            set
            {
                if (value < 5 || value > 8)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (IsOpen)
                    _stream?.SetAttributes(_baudRate, _parity, value, _stopBits, _handshake);

                _dataBits = value;
            }
        }

        //[MonoTODO("Not implemented")]
        [Browsable(true)]
        [MonitoringDescription("")]
        [DefaultValue(false)]
        public bool DiscardNull
        {
            get => throw new NotImplementedException();
            set => throw
                // LAMESPEC: Msdn states that an InvalidOperationException exception
                // is fired if the port is not open, which is *not* happening.
                new NotImplementedException();
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        public bool DsrHolding
        {
            get
            {
                CheckOpen();
                return (_stream?.GetSignals() & SerialSignal.Dsr) != 0;
            }
        }

        [DefaultValueAttribute(false)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public bool DtrEnable
        {
            get => _dtrEnable;
            set
            {
                if (value == _dtrEnable)
                    return;
                if (IsOpen)
                    _stream?.SetSignal(SerialSignal.Dtr, value);

                _dtrEnable = value;
            }
        }

        [Browsable(false)]
        [DesignerSerializationVisibilityAttribute(DesignerSerializationVisibility.Hidden)]
        [MonitoringDescription("")]
        public Encoding Encoding
        {
            get => _encoding;
            set => _encoding = value ?? throw new ArgumentNullException(nameof(value));
        }

        [DefaultValueAttribute(Handshake.None)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public Handshake Handshake
        {
            get => _handshake;
            set
            {
                if (value < Handshake.None || value > Handshake.RequestToSendXOnXOff)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (IsOpen)
                    _stream?.SetAttributes(_baudRate, _parity, _dataBits, _stopBits, value);

                _handshake = value;
            }
        }

        [Browsable(false)]
        public bool IsOpen { get; private set; }

        [DefaultValueAttribute("\n")]
        [Browsable(false)]
        [MonitoringDescription("")]
        public string NewLine
        {
            get => _newLine;
            set
            {
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (value.Length == 0)
                    throw new ArgumentException("NewLine cannot be null or empty.", nameof(value));

                _newLine = value;
            }
        }

        [DefaultValueAttribute(DefaultParity)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public Parity Parity
        {
            get => _parity;
            set
            {
                if (value < Parity.None || value > Parity.Space)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (IsOpen)
                    _stream?.SetAttributes(_baudRate, value, _dataBits, _stopBits, _handshake);

                _parity = value;
            }
        }

        //[MonoTODO("Not implemented")]
        [Browsable(true)]
        [MonitoringDescription("")]
        [DefaultValue(63)]
        public byte ParityReplace
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }


        [Browsable(true)]
        [MonitoringDescription("")]
        [DefaultValue("COM1")] // silly Windows-ism. We should ignore it.
        public string PortName
        {
            get => _portName;
            set
            {
                if (IsOpen)
                    throw new InvalidOperationException("Port name cannot be set while port is open.");
                if (value == null)
                    throw new ArgumentNullException(nameof(value));
                if (value.Length == 0 || value.StartsWith(@"\\"))
                    throw new ArgumentException(null, nameof(value));

                _portName = value;
            }
        }

        [DefaultValueAttribute(DefaultReadBufferSize)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int ReadBufferSize
        {
            get => _readBufferSize;
            set
            {
                if (IsOpen)
                    throw new InvalidOperationException();
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (value <= DefaultReadBufferSize)
                    return;

                _readBufferSize = value;
            }
        }

        [DefaultValueAttribute(InfiniteTimeout)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int ReadTimeout
        {
            get => _readTimeout;
            set
            {
                if (value < 0 && value != InfiniteTimeout)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (IsOpen && _stream != null)
                    _stream.ReadTimeout = value;

                _readTimeout = value;
            }
        }

        //[MonoTODO("Not implemented")]
        [DefaultValueAttribute(1)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int ReceivedBytesThreshold
        {
            get => throw new NotImplementedException();
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                throw new NotImplementedException();
            }
        }

        [DefaultValueAttribute(false)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public bool RtsEnable
        {
            get => _rtsEnable;
            set
            {
                if (value == _rtsEnable)
                    return;
                if (IsOpen)
                    _stream?.SetSignal(SerialSignal.Rts, value);

                _rtsEnable = value;
            }
        }

        [DefaultValueAttribute(DefaultStopBits)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public StopBits StopBits
        {
            get => _stopBits;
            set
            {
                if (value < StopBits.One || value > StopBits.OnePointFive)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (IsOpen)
                    _stream?.SetAttributes(_baudRate, _parity, _dataBits, value, _handshake);

                _stopBits = value;
            }
        }

        [DefaultValueAttribute(DefaultWriteBufferSize)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int WriteBufferSize
        {
            get => _writeBufferSize;
            set
            {
                if (IsOpen)
                    throw new InvalidOperationException();
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (value <= DefaultWriteBufferSize)
                    return;

                _writeBufferSize = value;
            }
        }

        [DefaultValueAttribute(InfiniteTimeout)]
        [Browsable(true)]
        [MonitoringDescription("")]
        public int WriteTimeout
        {
            get => _writeTimeout;
            set
            {
                if (value < 0 && value != InfiniteTimeout)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (IsOpen && _stream != null)
                    _stream.WriteTimeout = value;

                _writeTimeout = value;
            }
        }

        // methods

        public void Close()
        {
            CloseStream();
        }

        private void CloseStream()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            _stream?.Close();
            _stream = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseStream();
            }
            else
            {
                IsOpen = false;
                _stream = null;
            }

            base.Dispose(disposing);
        }

        public void DiscardInBuffer()
        {
            CheckOpen();
            _stream?.DiscardInBuffer();
        }

        public void DiscardOutBuffer()
        {
            CheckOpen();
            _stream?.DiscardOutBuffer();
        }

        public static string[] GetPortNames()
        {
            List<string> serialPorts;

            // Are we on Unix?
            if (IsLinux)
            {
                serialPorts = new List<string>();

                var ttys = Directory.GetFiles("/dev/", "tty*");
                var linuxStyle = ttys.Any(dev => dev.StartsWith("/dev/ttyS") || dev.StartsWith("/dev/ttyUSB") || dev.StartsWith("/dev/ttyACM"));

                //
                // Probe for Linux-styled devices: /dev/ttyS* or /dev/ttyUSB*
                //

                foreach (var dev in ttys)
                {
                    if (linuxStyle)
                    {
                        if (dev.StartsWith("/dev/ttyS") || dev.StartsWith("/dev/ttyUSB") || dev.StartsWith("/dev/ttyACM"))
                            serialPorts.Add(dev);
                    }
                    else
                    {
                        if (dev != "/dev/tty" && dev.StartsWith("/dev/tty") && !dev.StartsWith("/dev/ttyC"))
                            serialPorts.Add(dev);
                    }
                }
            }
            else if (IsWindows)
            {
                serialPorts = WindowsComPortsEnumerator.GetPorts();
            }
            else
            {
                // what is this
                serialPorts = new List<string>();
            }

            return serialPorts.ToArray();
        }

        private static bool IsLinux
        {
            get
            {
                var id = Environment.OSVersion.Platform;
                return id == PlatformID.Unix || id == PlatformID.MacOSX || (int)id == 128; // 4, 6, but what is 128?
            }
        }

        private static bool IsWindows
        {
            get
            {
                var id = Environment.OSVersion.Platform;
                return id == PlatformID.Win32Windows || id == PlatformID.Win32NT; // WinCE not supported
            }
        }

        public void Open()
        {
            if (IsOpen)
                throw new InvalidOperationException("Port is already open");

            if (IsWindows) // Use windows kernel32 backend
                _stream = new WinSerialStream(_portName, _baudRate, _dataBits, _parity, _stopBits, _dtrEnable,
                    _rtsEnable, _handshake, _readTimeout, _writeTimeout, _readBufferSize, _writeBufferSize);
            else // Use standard unix backend
                _stream = new SerialPortStream(_portName, _baudRate, _dataBits, _parity, _stopBits, _dtrEnable,
                    _rtsEnable, _handshake, _readTimeout, _writeTimeout, _readBufferSize, _writeBufferSize);

            IsOpen = true;
        }

        public int Read(Span<byte> buffer)
        {
            CheckOpen();

            if (buffer.Length == 0)
                return 0;

            if (buffer.Length < 0)
                throw new ArgumentOutOfRangeException(nameof(buffer),"buffer length less than zero.");

            return _stream?.Read(buffer) ?? throw new NullReferenceException("stream is null");
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return this.Read(buffer.AsSpan(offset, count));
        }

        public int Read(char[] buffer, int offset, int count)
        {
            CheckOpen();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 )
                throw new ArgumentOutOfRangeException(nameof(offset), $"offset less than zero, count = {count}.");

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), $"count less than zero, offset = {offset}.");

            if (buffer.Length - offset < count)
                throw new ArgumentOutOfRangeException(nameof(buffer), "buffer is less than offset + count.");

            int c, i;
            for (i = 0; i < count && (c = ReadChar()) != -1; i++)
                buffer[offset + i] = (char)c;

            return i;
        }

        private readonly byte[] _readByteBuff = new byte[1];

        private int ReadByteInternal()
        {
            if (_stream?.Read(_readByteBuff, 0, 1) > 0)
                return _readByteBuff[0];

            return -1;
        }

        public int ReadByte()
        {
            CheckOpen();
            return ReadByteInternal();
        }

        public int ReadChar()
        {
            CheckOpen();

            var buffer = new byte[16];
            var i = 0;

            do
            {
                var b = ReadByteInternal();
                if (b == -1)
                    return -1;
                buffer[i++] = (byte)b;
                var c = _encoding.GetChars(buffer, 0, 1);
                if (c.Length > 0)
                    return (int)c[0];
            } while (i < buffer.Length);

            return -1;
        }

        public string ReadExisting()
        {
            CheckOpen();

            var count = BytesToRead;
            var bytes = new byte[count];

            if (_stream == null)
                throw new NullReferenceException("stream is null");

            var n = _stream.Read(bytes, 0, count);
            return new string(_encoding.GetChars(bytes, 0, n));
        }

        public string ReadLine()
        {
            return ReadTo(_newLine);
        }

        public string ReadTo(string value)
        {
            CheckOpen();
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length == 0)
                throw new ArgumentException("value");

            // Turn into byte array, so we can compare
            var byteValue = _encoding.GetBytes(value);
            var current = 0;
            var seen = new List<byte>();

            while (true)
            {
                var n = ReadByteInternal();
                if (n == -1)
                    break;
                seen.Add((byte)n);
                if (n == byteValue[current])
                {
                    current++;
                    if (current == byteValue.Length)
                        return _encoding.GetString(seen.ToArray(), 0, seen.Count - byteValue.Length);
                }
                else
                {
                    current = (byteValue[0] == n) ? 1 : 0;
                }
            }
            return _encoding.GetString(seen.ToArray());
        }

        public void Write(string text)
        {
            CheckOpen();
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var buffer = _encoding.GetBytes(text);
            Write(buffer, 0, buffer.Length);
        }

        public void Write(ReadOnlySpan<byte> buffer)
        {
            CheckOpen();

            if (buffer.Length < 0)
                throw new ArgumentOutOfRangeException(nameof(buffer), "buffer length less than zero.");

            _stream?.Write(buffer);
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            CheckOpen();
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 )
                throw new ArgumentOutOfRangeException(nameof(offset), $"offset less than zero, count = {count}.");

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), $"count less than zero, offset = {offset}.");

            if (buffer.Length - offset < count)
                throw new ArgumentOutOfRangeException(nameof(buffer), "buffer is less than offset + count.");

            _stream?.Write(buffer, offset, count);
        }

        public void Write(char[] buffer, int offset, int count)
        {
            CheckOpen();
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 )
                throw new ArgumentOutOfRangeException(nameof(offset), $"offset less than zero, count = {count}.");

            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), $"count less than zero, offset = {offset}.");

            if (buffer.Length - offset < count)
                throw new ArgumentOutOfRangeException(nameof(buffer), "buffer is less than offset + count.");

            byte[] bytes = _encoding.GetBytes(buffer, offset, count);
            _stream?.Write(bytes, 0, bytes.Length);
        }

        public void WriteLine(string text)
        {
            Write(text + _newLine);
        }

        private void CheckOpen()
        {
            if (!IsOpen)
                throw new InvalidOperationException("Specified port is not open.");
        }

        internal void OnErrorReceived(SerialErrorReceivedEventArgs args)
        {
            var handler =
                (SerialErrorReceivedEventHandler)Events[_errorReceived];

            handler?.Invoke(this, args);
        }

        internal void OnDataReceived(SerialDataReceivedEventArgs args)
        {
            var handler =
                (SerialDataReceivedEventHandler)Events[_dataReceived];

            handler?.Invoke(this, args);
        }

        internal void OnDataReceived(SerialPinChangedEventArgs args)
        {
            var handler =
                (SerialPinChangedEventHandler)Events[_pinChanged];

            handler?.Invoke(this, args);
        }

        // events
        [MonitoringDescription("")]
        public event SerialErrorReceivedEventHandler ErrorReceived
        {
            add => Events.AddHandler(_errorReceived, value);
            remove => Events.RemoveHandler(_errorReceived, value);
        }

        [MonitoringDescription("")]
        public event SerialPinChangedEventHandler PinChanged
        {
            add => Events.AddHandler(_pinChanged, value);
            remove => Events.RemoveHandler(_pinChanged, value);
        }

        [MonitoringDescription("")]
        public event SerialDataReceivedEventHandler DataReceived
        {
            add => Events.AddHandler(_dataReceived, value);
            remove => Events.RemoveHandler(_dataReceived, value);
        }
    }

    public delegate void SerialDataReceivedEventHandler(object sender, SerialDataReceivedEventArgs e);
    public delegate void SerialPinChangedEventHandler(object sender, SerialPinChangedEventArgs e);
    public delegate void SerialErrorReceivedEventHandler(object sender, SerialErrorReceivedEventArgs e);
}

