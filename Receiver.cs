using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Linq;

static class Receiver
{
    [StructLayout(LayoutKind.Sequential)]
    struct InterfaceData { public int Size; public Guid Class; public int Flags; public IntPtr Reserved; }
    [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError=true)]
    static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint needed, IntPtr device);
    [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    public static string Find()
    {
        var endpoint = KeyboardProfiles.Select(KeyboardProfiles.Get("f75"), Scan(), "");
        return endpoint == null ? null : endpoint.Path;
    }

    public static List<HidEndpoint> Scan()
    {
        var endpoints = new List<HidEndpoint>();
        Guid guid = new Guid("4d1e55b2-f16f-11cf-88cb-001111000030");
        IntPtr set = SetupDiGetClassDevs(ref guid, null, IntPtr.Zero, 18);
        if (set == new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            for (uint i=0; ; i++)
            {
                InterfaceData data = new InterfaceData(); data.Size = Marshal.SizeOf(data);
                if (!SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref data)) break;
                uint needed;
                SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out needed, IntPtr.Zero);
                IntPtr detail = Marshal.AllocHGlobal((int)needed);
                try
                {
                    Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(set, ref data, detail, needed, out needed, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    var endpoint = HidTransport.Inspect(path);
                    if (endpoint != null) endpoints.Add(endpoint);
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
            return endpoints;
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }
}


