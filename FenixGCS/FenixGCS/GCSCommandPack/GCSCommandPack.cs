using System;
using MemoryPack;

namespace FenixGCSApi
{
    [MemoryPackUnion((int)EMsgType.Login, typeof(GCSCommand_Login_Request))]
    [MemoryPackUnion((int)EMsgType.LoginRtn, typeof(GCSCommand_Login_Response))]
    [MemoryPackable]
    public abstract partial class GCSCommandPack
    {
        public EMsgType EMsgType { get; set; }
        public string ID { get; set; }
        public bool IsRequest { get; set; }
        public string ResponseTo { get; set; }

        public GCSCommandPack(EMsgType eMsgType, bool isRequest = false, string responseTo = null)
        {
            ID = GUIDGetter.Get();
            EMsgType = eMsgType;
            IsRequest = isRequest;
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
    }
}
