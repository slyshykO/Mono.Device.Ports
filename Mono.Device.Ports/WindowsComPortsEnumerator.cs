using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Mono.IO.Ports
{
    partial class WindowsComPortsEnumerator
    {
#if NET7_0_OR_GREATER
        [LibraryImport("advapi32.dll", EntryPoint = "RegOpenKeyExW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int RegOpenKeyEx(
            IntPtr hKey,
            string lpSubKey,
            int ulOptions,
            int samDesired,
            out IntPtr hkResult);
#else
        [DllImport("advapi32.dll", EntryPoint = "RegOpenKeyExW", CharSet = CharSet.Unicode, BestFitMapping = false)]
        private static extern int RegOpenKeyEx(
            IntPtr hKey,
            string lpSubKey,
            int ulOptions,
            int samDesired,
            out IntPtr hkResult);
#endif

#if NET7_0_OR_GREATER
        [LibraryImport("advapi32.dll")]
        private static partial int RegCloseKey(IntPtr hKey);
#else
        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(IntPtr hKey);
#endif

#if NET7_0_OR_GREATER
        [LibraryImport("advapi32.dll", EntryPoint = "RegEnumValueW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int RegEnumValue(
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
        private static extern int RegEnumValue(
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
        private static partial int RegQueryValueEx(
            IntPtr hKey,
            string lpValueName,
            IntPtr lpReserved,
            ref int lpType,
            [Out] char[] lpData,
            ref int lpcbData);
#else
        [DllImport("advapi32.dll", EntryPoint = "RegQueryValueExW", CharSet = CharSet.Unicode, BestFitMapping = false)]
        private static extern int RegQueryValueEx(
            IntPtr hKey,
            string lpValueName,
            IntPtr lpReserved,
            ref int lpType,
            [Out] char[] lpData,
            ref int lpcbData);
#endif

        public static List<string> GetPorts()
        {
            var HKEY_LOCAL_MACHINE = new IntPtr(-2147483646);
            const int KEY_ENUMERATE_SUB_KEYS = 8;
            const int KEY_READ = 0x20019;
            const int REG_SZ = 1;

            var r = new List<string>();

            try
            {
                // if there are no serial ports at all, the registry key is not there at all
                var errorCode = RegOpenKeyEx(HKEY_LOCAL_MACHINE
                    , @"HARDWARE\DEVICEMAP\SERIALCOMM"
                    , 0
                    , KEY_ENUMERATE_SUB_KEYS | KEY_READ
                    , out var serialCommHkey);
                if (errorCode != 0)
                    return r;

                try
                {
                    var chArray = new char[300];

                    for (; ; )
                    {
                        var len = chArray.Length;
                        errorCode = RegEnumValue(serialCommHkey
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
                        errorCode = RegQueryValueEx(serialCommHkey, keyName, IntPtr.Zero, ref lpType, chArray, ref dataLen);

                        if (errorCode != 0 || lpType != REG_SZ) continue;
                        dataLen = dataLen / sizeof(char) - 1; // in bytes without zero terminator

                        if (dataLen <= 0) continue;
                        var valueString = new string(chArray, 0, dataLen);
                        r.Add(valueString);
                    }
                }
                finally
                {
                    RegCloseKey(serialCommHkey);
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
