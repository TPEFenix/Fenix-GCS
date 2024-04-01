using System;
using FenixGCSApi.Client;
using MemoryPack;

namespace FenixGCSApi
{
    [MemoryPackUnion((int)EMsgType.LoginHint, typeof(GCSCommand_LoginHint))]
    [MemoryPackUnion((int)EMsgType.Login, typeof(GCSCommand_Login_Request))]
    [MemoryPackUnion((int)EMsgType.LoginRtn, typeof(GCSCommand_Login_Response))]
    [MemoryPackable]
    public abstract partial class GCSCommandPack
    {
        public EMsgType EMsgType { get; set; }
        public string ID { get; set; }
        public bool IsRequest { get; set; }
        public string ResponseTo { get; set; }
        public ESendTunnelType TunnelType { get; set; }

        public GCSCommandPack(EMsgType eMsgType, bool isRequest = false, string responseTo = null, ESendTunnelType tunnelType = ESendTunnelType.TCP)
        {
            ID = GUIDGetter.Get();
            EMsgType = eMsgType;
            IsRequest = isRequest;
            TunnelType = tunnelType;
            ResponseTo = responseTo;
        }
        public byte[] Serialize()
        {
            return MemoryPackSerializer.Serialize(this);
        }
        public static GCSCommandPack Deserialize(byte[] data)
        {
            return MemoryPackSerializer.Deserialize<GCSCommandPack>(data);
        }
        public static T Deserialize<T>(byte[] data) where T : GCSCommandPack
        {
            var baseClass = Deserialize(data);
            if (baseClass == null)
                return null;
            if (baseClass is T)
                return (T)baseClass;
            else
                return null;
        }
    }
}
