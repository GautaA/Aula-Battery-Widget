using System;

// Protocol sources and validation tiers are documented in docs/compatibility.md.
public static class AulaBatteryProbe
{
    public sealed class Reading
    {
        public int? Percent;
        public bool PluggedIn;
        public string Message;
        public override string ToString() { return Message; }
    }

    public static byte[] Request13(byte command)
    {
        if(command!=0x4A && command!=0x0B) throw new ArgumentException("Only battery/status getters are allowed");
        byte[] query=new byte[20]; query[0]=0x13; query[1]=command; query[19]=(byte)(0x13+command); return query;
    }

    public static byte[] Request20()
    {
        // Windows adds the zero report-ID byte to the 32-byte payload.
        byte[] query=new byte[33]; query[1]=0x20; query[2]=1; query[32]=0x21; return query;
    }

    public static Reading Decode13(byte[] reply, byte command)
    {
        if(reply==null || reply.Length!=20 || reply[0]!=0x13 || (reply[1]&0x7F)!=command || reply[2]!=1 || reply[3]!=0) return null;
        int sum=0; for(int i=0;i<19;i++) sum+=reply[i];
        if((byte)sum!=reply[19]) return null;
        if(command==0x4A && reply[4]==2) {
            if(reply[6]==0x10) return new Reading { PluggedIn=true, Message="Plugged in; percentage unavailable" };
            if(reply[6]==1 && reply[5]>=1 && reply[5]<=100) return new Reading { Percent=reply[5], Message="On battery" };
        }
        // F99 Pro's documented 0x0B getter answers with a 0x0A information frame.
        // Only accept its known wireless connection state; do not infer charging.
        if(command==0x0A && reply[4]==4 && reply[7]==1 && reply[6]>=1 && reply[6]<=100)
            return new Reading { Percent=reply[6], Message="Reported level; charging status unavailable" };
        return null;
    }

    public static Reading Decode20(byte[] reply)
    {
        if(reply==null || reply.Length!=33 || reply[0]!=0 || reply[1]!=0x20 || reply[2]!=1 || reply[3]!=0) return null;
        int sum=0; for(int i=1;i<32;i++) sum+=reply[i];
        if((byte)sum!=reply[32] || reply[4]<1 || reply[4]>100) return null;
        return new Reading { Percent=reply[4], Message="Reported level; charging status unavailable" };
    }

    static Reading Exchange(HidTransport transport, byte[] request, Func<byte[],Reading> decode)
    {
        transport.Flush();
        if(transport.Transfer(request,700)==null) throw new InvalidOperationException("Battery query timed out");
        var clock=System.Diagnostics.Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds<700) {
            var reply=transport.Transfer(null,(uint)Math.Max(1,700-clock.ElapsedMilliseconds));
            if(reply==null) break;
            var reading=decode(reply); if(reading!=null) return reading;
        }
        return null;
    }

    public static string Read(string path) { var reading=Query(path); return (reading.Percent.HasValue ? reading.Percent + "%: " : "") + reading.Message; }
    public static Reading Query(string path) { return Query(path, KeyboardProfiles.Get("f75")); }

    public static Reading Query(string path, KeyboardProfile profile)
    {
        using(var transport=new HidTransport(path)) {
            if(!KeyboardProfiles.Accepts(profile,transport.Endpoint)) throw new InvalidOperationException("This receiver does not match the selected keyboard profile. No query sent.");
            for(int attempt=0;attempt<2;attempt++) {
                Reading reading;
                if(transport.Endpoint.Family=="20") reading=Exchange(transport,Request20(),Decode20);
                else reading=Exchange(transport,Request13(0x4A), r => Decode13(r,0x4A));
                if(reading!=null) return reading;
                if(attempt==0) System.Threading.Thread.Sleep(200);
            }
            if(profile.Id=="f99pro") {
                var reading=Exchange(transport,Request13(0x0B),r => Decode13(r,0x0A));
                if(reading!=null) return reading;
            }
            return new Reading { Message="No valid battery reply. Wake the keyboard or check its receiver and profile." };
        }
    }
}
