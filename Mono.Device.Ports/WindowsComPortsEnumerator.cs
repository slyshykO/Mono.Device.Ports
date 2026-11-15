using System;
using System.Collections.Generic;

namespace Mono.IO.Ports
{
#if NET6_0_OR_GREATER
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
    internal class WindowsComPortsEnumerator
    {
        public static List<string> GetPorts()
        {
            var r = new List<string>();

            try
            {
                // if there are no serial ports at all, the registry key is not there at all
                var errorCode = NativeWindows.RegOpenKeyEx(NativeWindows.HKeyLocalMachine
                    , @"HARDWARE\DEVICEMAP\SERIALCOMM"
                    , 0
                    , NativeWindows.KeyEnumerateSubKeys | NativeWindows.KeyRead
                    , out var serialCommHkey);
                if (errorCode != 0)
                    return r;

                try
                {
                    var chArray = new char[300];

                    for (; ; )
                    {
                        var len = chArray.Length;
                        errorCode = NativeWindows.RegEnumValue(serialCommHkey
                            , r.Count
                            , chArray
                            , ref len
                            , IntPtr.Zero
                            , IntPtr.Zero
                            , IntPtr.Zero
                            , IntPtr.Zero);
                        if (errorCode != 0)
                            break;

                        var keyName = new string(chArray, 0, len);

                        var lpType = 0;
                        var dataLen = chArray.Length * sizeof(char);
                        errorCode = NativeWindows.RegQueryValueEx(serialCommHkey, keyName, IntPtr.Zero, ref lpType, chArray, ref dataLen);

                        if (errorCode != 0 || lpType != NativeWindows.RegSz) continue;
                        dataLen = dataLen / sizeof(char) - 1; // in bytes without zero terminator

                        if (dataLen <= 0) continue;
                        var valueString = new string(chArray, 0, dataLen);
                        r.Add(valueString);
                    }
                }
                finally
                {
                    NativeWindows.RegCloseKey(serialCommHkey);
                }
            }
            catch
            {
                // paranoid catch.. if interop does not find the functions
            }

            return r;
        }
    }
}
