using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

// DeathAdder V4 Pro: 1532:00BE wired / 1532:00BF receiver, interface 0.
// Protocol reference (only power getters are used):
// https://github.com/va0z-code/razer-battery-tray-indicator/blob/main/src/controller.rs
// https://github.com/va0z-code/razer-battery-tray-indicator/blob/main/src/devices.rs
public static class RazerBatteryProbe
{
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll", SetLastError=true)]
    static extern bool HidD_SetFeature(SafeFileHandle handle, byte[] buffer, int length);
    [DllImport("hid.dll", SetLastError=true)]
    static extern bool HidD_GetFeature(SafeFileHandle handle, [In, Out] byte[] buffer, int length);

    public static byte[] Request(byte command)
    {
        if (command != 0x80 && command != 0x84) throw new ArgumentException("Only battery getters are supported");
        byte[] report = new byte[91]; // Report ID 0 followed by the 90-byte Razer packet.
        report[2] = 0x1F;
        report[6] = 2;
        report[7] = 7;
        report[8] = command;
        for (int i=3; i<89; i++) report[89] ^= report[i];
        return report;
    }

    public static byte Decode(byte[] reply, byte command)
    {
        if (reply == null || reply.Length != 91 || reply[0] != 0 || reply[2] != 0x1F ||
            reply[3] != 0 || reply[4] != 0 || reply[5] != 0 || reply[6] != 2 || reply[7] != 7 || reply[8] != command)
            throw new InvalidOperationException("Battery response did not match the query");
        byte checksum = 0;
        for (int i=3; i<89; i++) checksum ^= reply[i];
        if (reply[89] != checksum) throw new InvalidOperationException("Battery response checksum failed");
        if (reply[1] == 1) throw new InvalidOperationException("Mouse is busy; will retry on the next refresh");
        if (reply[1] == 4) throw new InvalidOperationException("Mouse may be asleep or disconnected");
        if (reply[1] == 5) throw new InvalidOperationException("Battery query is not supported on this connection");
        if (reply[1] != 2) throw new InvalidOperationException("Mouse did not provide a battery reading");
        if (command == 0x84 && reply[10] > 1) throw new InvalidOperationException("Unknown charging state");
        return reply[10];
    }

    static byte Exchange(SafeFileHandle handle, byte command)
    {
        Exception last = null;
        for (int attempt=0; attempt<2; attempt++) {
            byte[] query = Request(command);
            if (!HidD_SetFeature(handle, query, query.Length)) throw new Win32Exception(Marshal.GetLastWin32Error());
            Thread.Sleep(70);
            byte[] reply = new byte[91];
            if (!HidD_GetFeature(handle, reply, reply.Length)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try { return Decode(reply, command); }
            catch (InvalidOperationException ex) { last = ex; }
            if (attempt == 0) Thread.Sleep(200);
        }
        throw last;
    }

    public static AulaBatteryProbe.Reading Query(string path)
    {
        string normalized = path.ToLowerInvariant();
        if (!(normalized.Contains("vid_1532&pid_00bf&mi_00#") || normalized.Contains("vid_1532&pid_00be&mi_00#")))
            throw new ArgumentException("Not a supported DeathAdder V4 Pro control interface");
        // Windows reserves mouse input access. Feature reports work with a zero-access shared handle.
        using (var handle = CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero)) {
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            byte raw = Exchange(handle, 0x80);
            var reading = new AulaBatteryProbe.Reading { Percent = (raw * 100 + 127) / 255 };
            try { reading.Charging = Exchange(handle, 0x84) != 0; }
            catch (Win32Exception) { /* Level remains valid if the optional charging query fails. */ }
            catch (InvalidOperationException) { }
            reading.Message = reading.Charging.HasValue ? (reading.Charging.Value ? "Charging" : "On battery") : "Charging status unavailable";
            return reading;
        }
    }
}
