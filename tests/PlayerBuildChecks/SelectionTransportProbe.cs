using System.Collections.Concurrent;
using System.Threading;
using PacketDotNet;
using SharpPcap;

// Diagnostic only: packet counts and lengths, never packet contents or addresses.
internal static class SelectionTransportProbe
{
    public static void Run(string portList,int seconds)
    {
        if(seconds is <5 or >120)throw new ArgumentOutOfRangeException(nameof(seconds));
        var ports=portList.Split(',').Select(ushort.Parse).ToHashSet();
        var counts=new ConcurrentDictionary<string,long>();
        var captures=new List<ICaptureDevice>();
        foreach(var device in CaptureDeviceList.Instance)
        {
            try
            {
                device.Open(DeviceModes.Promiscuous,100);
                device.Filter="udp and ("+string.Join(" or ",ports.Select(p=>$"port {p}"))+")";
                device.OnPacketArrival+=(_,capture)=>
                {
                    var raw=capture.GetPacket();
                    var udp=Packet.ParsePacket(raw.LinkLayerType,raw.Data).Extract<UdpPacket>();
                    if(udp is null)return;
                    var direction=ports.Contains(udp.SourcePort)?"out":"in";
                    var port=direction=="out"?udp.SourcePort:udp.DestinationPort;
                    counts.AddOrUpdate($"{direction}:{port}:length={udp.PayloadData.Length}",1,(_,n)=>n+1);
                };
                device.StartCapture();captures.Add(device);
            }
            catch { try{device.Close();}catch{} }
        }
        Console.WriteLine($"READY interfaces={captures.Count} duration={seconds}s");
        try
        {
            for(var elapsed=0;elapsed<seconds;elapsed+=5)
            {
                Thread.Sleep(Math.Min(5,seconds-elapsed)*1000);
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{ElapsedSeconds=Math.Min(elapsed+5,seconds),Counts=counts.OrderBy(x=>x.Key).ToDictionary()}));
            }
        }
        finally { foreach(var device in captures){device.StopCapture();device.Close();} }
    }
}
