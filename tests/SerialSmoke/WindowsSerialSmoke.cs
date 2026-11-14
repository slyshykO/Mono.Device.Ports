// Copyright (c) 2026 Oleksiy Slyshyk
// SPDX-License-Identifier: MIT

using Mono.IO.Ports;
using Microsoft.Win32;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
internal static class WindowsSerialSmoke
{
    public static void Run()
    {
        string[] names = SerialPort.GetPortNames();
        Check(names.All(name => !string.IsNullOrWhiteSpace(name)), "Enumeration returned an empty port name");
        // Compare against an independent registry reader so a marshalling failure
        // cannot silently pass by returning an empty or corrupted port list.
        using (var serialComm = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM"))
        {
            string[] expected = serialComm == null ? Array.Empty<string>() : serialComm.GetValueNames()
                .Where(name => serialComm.GetValueKind(name) == RegistryValueKind.String)
                .Select(name => serialComm.GetValue(name) as string)
                .Where(value => !string.IsNullOrEmpty(value))
                .Select(value => value!)
                .ToArray();
            Check(names.OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(expected.OrderBy(name => name, StringComparer.Ordinal)),
                "Port enumeration did not match the registry");
        }

        // Exercise the Win32 open/error path without opening a physical device.
        string missingPort = $"MonoDevicePortsSmokeMissing-{Guid.NewGuid():N}";
        using var port = new SerialPort(missingPort, 115200, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 100,
            WriteTimeout = 100,
            Handshake = Handshake.None
        };
        Check(!port.IsOpen, "New port should be closed");
        Check(port.PortName == missingPort && port.BaudRate == 115200 && port.DataBits == 8 &&
              port.Parity == Parity.None && port.StopBits == StopBits.One &&
              port.Handshake == Handshake.None && port.ReadTimeout == 100 && port.WriteTimeout == 100,
              "Port configuration was not retained");

        for (int i = 0; i < 3; i++)
        {
            Expect<IOException>(port.Open, "Opening a nonexistent device should fail");
            Check(!port.IsOpen, "Failed open should leave the port closed");
        }

        byte[] buffer = new byte[1];
        Expect<InvalidOperationException>(() => port.Read(buffer, 0, buffer.Length),
            "Closed port should reject reads");
        Expect<InvalidOperationException>(() => port.Write(buffer, 0, buffer.Length),
            "Closed port should reject writes");
        port.Close();
        port.Dispose();
        Check(!port.IsOpen, "Disposed port should be closed");

        Console.WriteLine($"Windows serial smoke passed: enumeration ({names.Length} ports), configuration, " +
            "missing-device errors, closed-port guards, disposal. Physical serial I/O was not tested.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Expect<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new Exception(message);
    }
}
