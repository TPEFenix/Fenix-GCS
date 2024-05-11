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
    [MemoryPackUnion(31, typeof(GCSPack_JoinRoomRequest))]
    public partial class GCSPack { }
    #endregion

    [MemoryPackable]
    public partial class GCSPack_CreateRoomRequest : GCSPack, IGCSRequestPack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required string RoomID { get; set; }
        public required string RoomInfo { get; set; }
        public required int MaxMemberCount { get; set; }
    }

    [MemoryPackable]
    public partial class GCSPack_JoinRoomRequest : GCSPack, IGCSRequestPack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required string RoomID { get; set; }
    }

}
