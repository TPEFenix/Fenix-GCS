using System;
using FenixGCSApi.Client;
using MemoryPack;

namespace FenixGCSApi
{
    [MemoryPackUnion(0, typeof(GCSRequestPack))]
    [MemoryPackUnion(1, typeof(GCSResponsePack))]
    [MemoryPackable]
    public abstract partial class GCSPack
    {
        public required string SenderID { get; set; }
        public string PackID { get; set; } = GUIDGetter.Get();
        public virtual ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.UDP;

        public byte[] Serialize()
        {
            return MemoryPackSerializer.Serialize(this);
        }
        public static GCSPack Deserialize(byte[] data)
        {
            return MemoryPackSerializer.Deserialize<GCSPack>(data);
        }
        public static T Deserialize<T>(byte[] data) where T : GCSPack
        {
            var baseClass = Deserialize(data);
            if (baseClass == null)
                return null;
            if (baseClass is T)
                return (T)baseClass;
            else
                return null;
        }

        public static implicit operator byte[](GCSPack commandPack)
        {
            return commandPack.Serialize();
        }
        public static explicit operator GCSPack(byte[] data)
        {
            return Deserialize(data);
        }
    }

    [MemoryPackable]
    public abstract partial class GCSRequestPack : GCSPack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
    }

    [MemoryPackable]
    public abstract partial class GCSResponsePack : GCSPack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required bool Success { get; set; }
        public required string ResponseTo { get; set; }
    }
}
