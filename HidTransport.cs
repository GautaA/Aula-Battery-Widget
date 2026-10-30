using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public sealed class HidEndpoint
{
    public string Path, Product;
    public ushort Vendor, ProductId, UsagePage, Usage, InputLength, OutputLength;
    public string Family {
        get {
            if (Vendor==0x3554 && ProductId==0xFA09 && UsagePage==0xFF02 && Usage==2 && InputLength==20 && OutputLength==20) return "13";
            if (((Vendor==0x05AC && ProductId==0x024F) || (Vendor==0x0C45 && ProductId==0xFEFE)) && UsagePage==0xFF60 && InputLength==33 && OutputLength==33) return "20";
            return null;
        }
    }
    public override string ToString() { return String.Format("{0:X4}:{1:X4}  {2}  usage {3:X4}:{4:X4}  in/out {5}/{6}",Vendor,ProductId,Product,UsagePage,Usage,InputLength,OutputLength); }
}

public sealed class HidTransport : IDisposable
{
    readonly SafeFileHandle handle;
    public HidEndpoint Endpoint { get; private set; }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
    [DllImport("hid.dll")] static extern bool HidD_GetPreparsedData(SafeFileHandle h,out IntPtr data);
    [DllImport("hid.dll")] static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] static extern int HidP_GetCaps(IntPtr data,IntPtr caps);
    [DllImport("hid.dll")] static extern bool HidD_GetAttributes(SafeFileHandle h,IntPtr attributes);
    [DllImport("hid.dll",CharSet=CharSet.Unicode)] static extern bool HidD_GetProductString(SafeFileHandle h,StringBuilder text,int bytes);
    [DllImport("hid.dll")] static extern bool HidD_FlushQueue(SafeFileHandle h);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadFile(SafeFileHandle h,IntPtr data,int size,out int count,IntPtr ov);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool WriteFile(SafeFileHandle h,IntPtr data,int size,out int count,IntPtr ov);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetOverlappedResult(SafeFileHandle h,IntPtr ov,out int count,bool wait);
    [DllImport("kernel32.dll")] static extern bool CancelIoEx(SafeFileHandle h,IntPtr ov);
    [DllImport("kernel32.dll")] static extern IntPtr CreateEvent(IntPtr attributes,bool manual,bool initial,string name);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr h,uint ms);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

    static HidEndpoint Describe(SafeFileHandle h,string path) {
        IntPtr pp;
        if(!HidD_GetPreparsedData(h,out pp)) return null;
        IntPtr caps=Marshal.AllocHGlobal(64), attrs=Marshal.AllocHGlobal(12);
        try {
            Marshal.Copy(new byte[12],0,attrs,12); Marshal.WriteInt32(attrs,12);
            if(HidP_GetCaps(pp,caps)!=0x110000 || !HidD_GetAttributes(h,attrs)) return null;
            var product=new StringBuilder(128); HidD_GetProductString(h,product,256);
            return new HidEndpoint { Path=path, Product=product.ToString(), Vendor=(ushort)Marshal.ReadInt16(attrs,4), ProductId=(ushort)Marshal.ReadInt16(attrs,6), Usage=(ushort)Marshal.ReadInt16(caps,0), UsagePage=(ushort)Marshal.ReadInt16(caps,2), InputLength=(ushort)Marshal.ReadInt16(caps,4), OutputLength=(ushort)Marshal.ReadInt16(caps,6) };
        } finally { Marshal.FreeHGlobal(caps); Marshal.FreeHGlobal(attrs); HidD_FreePreparsedData(pp); }
    }
    public static HidEndpoint Inspect(string path) {
        using(var h=CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero)) return h.IsInvalid ? null : Describe(h,path);
    }
    public HidTransport(string path) {
        handle=CreateFile(path,0xC0000000,3,IntPtr.Zero,3,0x40000000,IntPtr.Zero);
        if(handle.IsInvalid) { int error=Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        Endpoint=Describe(handle,path);
        if(Endpoint==null || Endpoint.Family==null) { handle.Dispose(); throw new InvalidOperationException("Unrecognized receiver. No query sent."); }
    }
    public void Flush() { HidD_FlushQueue(handle); }
    public byte[] Transfer(byte[] output,uint timeout) {
        int length=output==null ? Endpoint.InputLength : Endpoint.OutputLength;
        if(output!=null && output.Length!=length) throw new ArgumentException("Incorrect report length");
        IntPtr buffer=Marshal.AllocHGlobal(length);
        int size=IntPtr.Size==8?32:20;
        IntPtr ov=Marshal.AllocHGlobal(size), ev=CreateEvent(IntPtr.Zero,true,false,null);
        try {
            Marshal.Copy(new byte[size],0,ov,size);
            Marshal.WriteIntPtr(ov,IntPtr.Size==8?24:16,ev);
            if(output!=null) Marshal.Copy(output,0,buffer,length);
            int count;
            bool ok=output==null?ReadFile(handle,buffer,length,out count,ov):WriteFile(handle,buffer,length,out count,ov);
            if(!ok) {
                int error=Marshal.GetLastWin32Error(); if(error!=997) throw new Win32Exception(error);
                if(WaitForSingleObject(ev,timeout)!=0) { CancelIoEx(handle,ov); GetOverlappedResult(handle,ov,out count,true); return null; }
                if(!GetOverlappedResult(handle,ov,out count,false)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if(count!=length) return null;
            byte[] result=new byte[count]; Marshal.Copy(buffer,result,0,count); return result;
        } finally { CloseHandle(ev); Marshal.FreeHGlobal(ov); Marshal.FreeHGlobal(buffer); }
    }
    public void Dispose() { handle.Dispose(); }
}
