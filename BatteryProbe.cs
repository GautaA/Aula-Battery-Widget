using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// Read-only battery query documented at:
// https://github.com/ushineko/sanshoku/blob/main/specs/013-aula-f75.md
public static class AulaBatteryProbe
{
    public sealed class Reading
    {
        public int? Percent;
        public bool PluggedIn;
        public bool? Charging;
        public string Message;
        public override string ToString() { return Message; }
    }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data, IntPtr caps);
    [DllImport("hid.dll")] static extern bool HidD_FlushQueue(SafeFileHandle h);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool ReadFile(SafeFileHandle h, IntPtr data, int length, out int count, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool WriteFile(SafeFileHandle h, IntPtr data, int length, out int count, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool GetOverlappedResult(SafeFileHandle h, IntPtr overlapped, out int count, bool wait);
    [DllImport("kernel32.dll")] static extern bool CancelIoEx(SafeFileHandle h, IntPtr overlapped);
    [DllImport("kernel32.dll")] static extern IntPtr CreateEvent(IntPtr attributes, bool manual, bool initial, string name);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    static byte[] Transfer(SafeFileHandle h, byte[] output, uint timeout)
    {
        IntPtr buffer = Marshal.AllocHGlobal(20);
        int size = IntPtr.Size == 8 ? 32 : 20;
        IntPtr ov = Marshal.AllocHGlobal(size);
        IntPtr ev = CreateEvent(IntPtr.Zero, true, false, null);
        try
        {
            Marshal.Copy(new byte[size], 0, ov, size);
            Marshal.WriteIntPtr(ov, IntPtr.Size == 8 ? 24 : 16, ev);
            if (output != null) Marshal.Copy(output, 0, buffer, 20);
            int count;
            bool ok = output == null ? ReadFile(h, buffer, 20, out count, ov) : WriteFile(h, buffer, 20, out count, ov);
            if (!ok)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 997) throw new Win32Exception(error);
                if (WaitForSingleObject(ev, timeout) != 0)
                {
                    CancelIoEx(h, ov);
                    GetOverlappedResult(h, ov, out count, true);
                    return null;
                }
                if (!GetOverlappedResult(h, ov, out count, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            byte[] result = new byte[count];
            Marshal.Copy(buffer, result, 0, count);
            return result;
        }
        finally { CloseHandle(ev); Marshal.FreeHGlobal(ov); Marshal.FreeHGlobal(buffer); }
    }

    public static string Read(string path)
    {
        return Query(path).Message;
    }

    public static Reading Query(string path)
    {
        if (path.IndexOf("vid_3554&pid_fa09", StringComparison.OrdinalIgnoreCase) < 0)
            throw new ArgumentException("Not the supported AULA receiver");
        using (SafeFileHandle h = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero))
        {
            if (h.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr pp;
            if (!HidD_GetPreparsedData(h, out pp)) throw new Exception("Cannot read HID descriptor");
            IntPtr caps = Marshal.AllocHGlobal(64);
            try
            {
                if (HidP_GetCaps(pp, caps) != 0x110000) throw new Exception("Invalid HID capabilities");
                if ((ushort)Marshal.ReadInt16(caps, 2) != 0xFF02 || Marshal.ReadInt16(caps, 0) != 2 ||
                    Marshal.ReadInt16(caps, 4) != 20 || Marshal.ReadInt16(caps, 6) != 20)
                    throw new Exception("Unexpected HID collection; no query sent");
            }
            finally { Marshal.FreeHGlobal(caps); HidD_FreePreparsedData(pp); }
            byte[] query = new byte[20]; query[0]=0x13; query[1]=0x4A; query[19]=0x5D;
            for (int attempt=0; attempt<2; attempt++)
            {
                HidD_FlushQueue(h);
                if (Transfer(h, query, 700) == null) throw new Exception("Query write timed out");
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (clock.ElapsedMilliseconds < 700)
                {
                    byte[] reply = Transfer(h, null, (uint)Math.Max(1, 700-clock.ElapsedMilliseconds));
                    if (reply == null) break;
                    if (reply.Length != 20 || reply[0] != 0x13 || (reply[1]&0x7F) != 0x4A) continue;
                    int sum=0; for (int i=0; i<19; i++) sum+=reply[i];
                    if ((byte)sum != reply[19]) continue;
                    string raw = BitConverter.ToString(reply);
                    if (reply[6] == 0x10) return new Reading { PluggedIn = true, Message = "Plugged in; percentage unavailable" };
                    if (reply[6] == 1 && reply[5] >= 1 && reply[5] <= 100)
                        return new Reading { Percent = reply[5], Message = "Battery: " + reply[5] + "% (on battery)" };
                    return new Reading { Message = "Unrecognized battery state. Raw: " + raw };
                }
                if (attempt == 0) System.Threading.Thread.Sleep(200);
            }
            return new Reading { Message = "No reply - keyboard may be asleep or disconnected" };
        }
    }
}
