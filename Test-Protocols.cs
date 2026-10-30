using System;

static class ProtocolTests
{
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    static byte[] Hex(string bytes) { return Array.ConvertAll(bytes.Split(' '), b => Convert.ToByte(b,16)); }
    static void Sum(byte[] packet,int start,int end) { int sum=0; for(int i=start;i<end;i++) sum+=packet[i]; packet[end]=(byte)sum; }
    public static void Run()
    {
        // Published F75 captures, plus the 17% reading observed on this machine.
        var battery=Hex("13 4a 01 00 02 61 01 00 00 00 00 00 00 00 00 00 00 00 00 c2");
        Check(AulaBatteryProbe.Decode13(battery,0x4A).Percent==97,"Published F75 reading");
        var cable=Hex("13 4a 01 00 02 64 10 00 00 00 00 00 00 00 00 00 00 00 00 d4");
        var reading=AulaBatteryProbe.Decode13(cable,0x4A);
        Check(reading.PluggedIn && !reading.Percent.HasValue,"Cable placeholder must never become 100%");
        var live=Hex("13 4a 01 00 02 11 01 00 00 00 00 00 00 00 00 00 00 00 00 72");
        Check(AulaBatteryProbe.Decode13(live,0x4A).Percent==17,"Local capture");
        battery[1]=0xCA; Sum(battery,0,19);
        Check(AulaBatteryProbe.Decode13(battery,0x4A).Percent==97,"High command bit");
        battery[5]=0; Sum(battery,0,19); Check(AulaBatteryProbe.Decode13(battery,0x4A)==null,"Zero is unavailable, not empty");
        battery[5]=101; Sum(battery,0,19); Check(AulaBatteryProbe.Decode13(battery,0x4A)==null,"Out of range");
        battery[5]=50; battery[6]=3; Sum(battery,0,19); Check(AulaBatteryProbe.Decode13(battery,0x4A)==null,"Unknown power state");
        live[5]++; Check(AulaBatteryProbe.Decode13(live,0x4A)==null,"Corrupt reading");
        Check(AulaBatteryProbe.Decode13(new byte[19],0x4A)==null,"Truncated report");

        // Fixtures derived from published wire layouts (not hardware captures).
        var info=Hex("13 0a 01 00 04 05 5a 01 00 00 00 00 00 00 00 00 00 00 00 82");
        Check(AulaBatteryProbe.Decode13(info,0x0A).Percent==90,"F99 Pro information frame");
        Check(AulaBatteryProbe.Decode13(info,0x4A)==null,"Unrelated status cannot answer battery query");
        info[7]=0x10; Sum(info,0,19); Check(AulaBatteryProbe.Decode13(info,0x0A)==null,"Unknown info connection state");
        var max=Hex("00 20 01 00 5b 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 7c");
        Check(AulaBatteryProbe.Decode20(max).Percent==91,"F75 Max/S75 Pro frame");
        max[3]=1; Sum(max,1,32); Check(AulaBatteryProbe.Decode20(max)==null,"Wrong sequence");
        max[3]=0; max[4]=0; Sum(max,1,32); Check(AulaBatteryProbe.Decode20(max)==null,"No level sentinel");
        max[4]=255; Sum(max,1,32); Check(AulaBatteryProbe.Decode20(max)==null,"Invalid level");
        max[4]=50; Check(AulaBatteryProbe.Decode20(max)==null,"Wrong checksum");
        Check(AulaBatteryProbe.Decode20(new byte[32])==null,"Windows report ID is required");
        Check(BitConverter.ToString(AulaBatteryProbe.Request13(0x4A))=="13-4A-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-5D","Getter bytes");
        var request=AulaBatteryProbe.Request20(); Check(request.Length==33 && request[0]==0 && request[1]==0x20 && request[2]==1 && request[32]==0x21,"Sonix report framing");
        bool refused=false; try { AulaBatteryProbe.Request13(0x02); } catch(ArgumentException) { refused=true; }
        Check(refused,"Reject setting commands");

        var shared=new HidEndpoint { Vendor=0x3554, ProductId=0xFA09, UsagePage=0xFF02, Usage=2, InputLength=20, OutputLength=20, Path="shared" };
        var sonix=new HidEndpoint { Vendor=0x05AC, ProductId=0x024F, UsagePage=0xFF60, InputLength=33, OutputLength=33, Path="sonix" };
        Check(KeyboardProfiles.Accepts(KeyboardProfiles.Get("f75"),shared),"F75 endpoint");
        Check(KeyboardProfiles.Accepts(KeyboardProfiles.Get("f99pro"),shared),"F99 Pro endpoint");
        Check(!KeyboardProfiles.Accepts(KeyboardProfiles.Get("f75max"),shared),"Don't send Max commands to a shared receiver");
        Check(KeyboardProfiles.Accepts(KeyboardProfiles.Get("s75pro"),sonix),"S75 variant");
        sonix.Vendor=0x0C45; sonix.ProductId=0xFEFE; Check(sonix.Family=="20","S75 receiver ID");
        sonix.UsagePage=0xFF59; Check(sonix.Family==null,"Reject wrong collection");
        sonix.UsagePage=0xFF60; sonix.OutputLength=65; Check(sonix.Family==null,"Reject wrong report size");
        sonix.OutputLength=33; sonix.ProductId=0x800A; Check(sonix.Family==null,"Wired protocol is not the receiver protocol");
        shared.ProductId=0xFA08; Check(shared.Family==null,"Bluetooth isn't the USB dongle");
        shared.ProductId=0xFA09;
        var second=new HidEndpoint { Vendor=0x3554, ProductId=0xFA09, UsagePage=0xFF02, Usage=2, InputLength=20, OutputLength=20, Path="second" };
        refused=false; try { KeyboardProfiles.Select(KeyboardProfiles.Get("f75"),new[]{shared,second},""); } catch(InvalidOperationException) { refused=true; }
        Check(refused,"Ambiguous receivers require selection");
        Check(KeyboardProfiles.Select(KeyboardProfiles.Get("f75"),new[]{shared,second},"second")==second,"Explicit receiver selection");
        Check(KeyboardProfiles.Select(KeyboardProfiles.Get("f75"),new[]{shared},"second")==null,"Unplugged selected receiver must not fall back to another keyboard");
        Check(KeyboardProfiles.All.Length==11,"All requested model profiles present");
        Console.WriteLine("PASS: battery frames, charging sentinel, checksums, bounds, endpoint guards, and receiver selection");
    }
    static int Main() { try { Run(); return 0; } catch(Exception ex) { Console.Error.WriteLine(ex.Message); return 1; } }
}
