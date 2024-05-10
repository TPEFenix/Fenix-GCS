using FenixGCSApi.Client;
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FenixGCSApi
{
    #region MemoryPackUnion
    [MemoryPackUnion(30, typeof(GCSPack_CreateRoomRequest))]
    [MemoryPackUnion(31, typeof(GCSPack_CreateRoomResponse))]
    public partial class GCSPack { }
    #endregion


    [MemoryPackable]
    public partial class GCSPack_CreateRoomRequest : GCSPack, IGCSRequestPack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required string RoomID { get; set; }
        public required string RoomInfo { get; set; }
    }

    [MemoryPackable]
    public partial class GCSPack_CreateRoomResponse : GCSPack, IGCSResponsePack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required bool Success { get; set; }
        public required string ResponseTo { get; set; }
        public string ResponseMsg { get; set; }
    }
}
