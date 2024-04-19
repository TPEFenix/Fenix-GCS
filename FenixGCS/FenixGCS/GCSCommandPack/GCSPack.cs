using System;
using FenixGCSApi.Client;
using MemoryPack;

namespace FenixGCSApi
{
    public interface IGCSPack 
    {
        string SenderID { get; set; }
        string PackID { get; set; }
        ESendTunnelType SendTunnelType { get; set; }
        byte[] Serialize();
    }
    public interface IGCSRequestPack: IGCSPack { }
    public interface IGCSResponsePack: IGCSPack
    {
        bool Success { get; set; }
        string ResponseTo { get; set; }
    }

    [MemoryPackUnion(0, typeof(FakeGCSPack))]
    [MemoryPackable]
    public abstract partial class GCSPack
    {
        public required string SenderID { get; set; }
        public string PackID { get; set; } = GUIDGetter.Get();
        public virtual ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.UDP;

        public virtual byte[] Serialize()
        {
            return MemoryPackSerializer.Serialize(this);
        }
        public static T Deserialize<T>(byte[] data) where T : GCSPack
        {
            return MemoryPackSerializer.Deserialize<T>(data);
        }

        public static implicit operator byte[](GCSPack commandPack)
        {
            return commandPack.Serialize();
        }
        public static explicit operator GCSPack(byte[] data)
        {
            return Deserialize<GCSPack>(data);
        }
    }
    [MemoryPackable]
    internal partial class FakeGCSPack : GCSPack { }
}
