// Copyright (c) 2026 Oleksiy Slyshyk
// SPDX-License-Identifier: MIT

using System.Diagnostics;
using System.Runtime.InteropServices;
using Mono.IO.Ports;

if (OperatingSystem.IsWindows())
{
    WindowsSerialSmoke.Run();
    return;
}

if (!OperatingSystem.IsLinux())
    throw new PlatformNotSupportedException("The serial smoke tests support Windows and Linux.");

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

int master = Posix.posix_openpt(2 | 0x100); // Linux O_RDWR | O_NOCTTY, test only.
Check(master >= 0, "Cannot open pseudo-terminal master");
try
{
    Check(Posix.grantpt(master) == 0 && Posix.unlockpt(master) == 0, "Cannot initialize pseudo-terminal");
    string name = Marshal.PtrToStringUTF8(Posix.ptsname(master))!;
    using var port = new SerialPort(name, 115200) { ReadTimeout = 100, WriteTimeout = 100 };
    port.Open();
    byte[] payload = { 0, 1, 0x11, 0x13, 0x80, 0xff };
    byte[] buffer = new byte[payload.Length];
    Check(Posix.write(master, payload, (nuint)payload.Length) == payload.Length, "Master write failed");
    int received = 0;
    while (received < buffer.Length)
        received += port.Read(buffer.AsSpan(received));
    Check(buffer.SequenceEqual(payload), "Managed read corrupted binary data");
    port.Write(payload.AsSpan());
    Check(Posix.read(master, buffer, (nuint)buffer.Length) == buffer.Length, "Master read failed");
    Check(buffer.SequenceEqual(payload), "Managed write corrupted binary data");
    port.BaudRate = 1000000;
    port.BaudRate = 123456;
    port.BaudRate = 115200;
    var elapsed = Stopwatch.StartNew();
    try { port.Read(buffer.AsSpan()); throw new Exception("Read should time out"); }
    catch (TimeoutException) { }
    Check(elapsed.ElapsedMilliseconds < 2000, "Read timeout exceeded its deadline");
    try { port.Write(new byte[1024 * 1024]); throw new Exception("Write should time out"); }
    catch (TimeoutException) { }
    port.DiscardOutBuffer();
    var stream = port.BaseStream!;
    port.Close();
    Check(!stream.CanRead && !stream.CanWrite, "Closed stream advertises I/O capability");
    try { stream.ReadByte(); throw new Exception("Closed stream should reject reads"); }
    catch (ObjectDisposedException) { }

    // Warm up exception paths before comparing the process's descriptor count.
    static void FailedOpen(string path, int baud)
    {
        using var invalid = new SerialPort(path, baud);
        try { invalid.Open(); throw new Exception("Invalid open should fail"); }
        catch (IOException) { }
    }
    FailedOpen(name, -1);
    FailedOpen("/dev/mono-device-ports-nonexistent", 9600);
    int before = Directory.GetFiles("/proc/self/fd").Length;
    for (int i = 0; i < 20; i++) FailedOpen(name, -1);
    Check(Directory.GetFiles("/proc/self/fd").Length == before, "Constructor failure leaked descriptors");

    port.Open();
    Check(Posix.close(master) == 0, "Master close failed");
    master = -1;
    try { port.Read(buffer.AsSpan()); throw new Exception("Disconnect should fail"); }
    catch (IOException) { }
    Console.WriteLine("Managed serial smoke passed: native loading, binary I/O, baud rates, timeouts, disposal, failed-open cleanup, disconnect.");
}
finally
{
    if (master >= 0) Posix.close(master);
}

internal static class Posix
{
    [DllImport("libc", SetLastError = true)] internal static extern int posix_openpt(int flags);
    [DllImport("libc", SetLastError = true)] internal static extern int grantpt(int fd);
    [DllImport("libc", SetLastError = true)] internal static extern int unlockpt(int fd);
    [DllImport("libc", SetLastError = true)] internal static extern IntPtr ptsname(int fd);
    [DllImport("libc", SetLastError = true)] internal static extern nint read(int fd, byte[] buffer, nuint count);
    [DllImport("libc", SetLastError = true)] internal static extern nint write(int fd, byte[] buffer, nuint count);
    [DllImport("libc", SetLastError = true)] internal static extern int close(int fd);
}
