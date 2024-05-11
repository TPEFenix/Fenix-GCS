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
    public interface IGCSRequestPack : IGCSPack { }
    public interface IGCSResponsePack : IGCSPack
    {
        bool Success { get; set; }
        string ResponseTo { get; set; }
        string ResponseMsg { get; set; }
    }

    [MemoryPackUnion(0, typeof(BinaryDataGCSPack))]
    [MemoryPackUnion(1, typeof(StringDataGCSPack))]
    [MemoryPackUnion(2, typeof(GCSPack_BasicResponse))]
    [MemoryPackable]
    public abstract partial class GCSPack : IGCSPack
    {
        //經過實測，用is去直接GetType判斷的速度遠比自己存一個Int去判斷Type還快
        public string SenderID { get; set; }
        public string PackID { get; set; } = GUIDGetter.Get();
        public virtual ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.UDP;

        public virtual byte[] Serialize()
        {
            return MemoryPackSerializer.Serialize(this);
        }
        public static T Deserialize<T>(byte[] data) where T : GCSPack
        {
            try
            {
                return MemoryPackSerializer.Deserialize<T>(data);
            }
            catch
            {
                return null;
            }
        }

        public static implicit operator byte[](GCSPack commandPack)
        {
            return commandPack.Serialize();
        }
        public static explicit operator GCSPack(byte[] data)
        {
            return Deserialize<GCSPack>(data);
        }

        public static GCSPack_BasicResponse GenerateBasicResponse(bool success, string responseTo, string senderID, string responseMsg = null)
        {
            GCSPack_BasicResponse response = new GCSPack_BasicResponse()
            {
                Success = success,
                ResponseTo = responseTo,
                SenderID = senderID,
                ResponseMsg = responseMsg,
            };
            return response;
        }
    }
    [MemoryPackable]
    public partial class BinaryDataGCSPack : GCSPack
    {
        public byte[] Data { get; set; }
    }
    [MemoryPackable]
    public partial class StringDataGCSPack : GCSPack
    {
        public string Data { get; set; }
    }

    [MemoryPackable]
    public partial class GCSPack_BasicResponse : GCSPack, IGCSResponsePack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required bool Success { get; set; }
        public required string ResponseTo { get; set; }
        public string ResponseMsg { get; set; }
    }

}
