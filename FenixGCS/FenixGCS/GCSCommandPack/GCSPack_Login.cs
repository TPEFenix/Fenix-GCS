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
    [MemoryPackUnion(10, typeof(GCSPack_LoginRequest))]
    [MemoryPackUnion(11, typeof(GCSPack_LoginResponse))]
    [MemoryPackUnion(12, typeof(GCSPack_LoginHint))]
    public partial class GCSPack { }
    #endregion


    [MemoryPackable]
    public partial class GCSPack_LoginRequest : GCSPack, IGCSRequestPack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required string UserID { get; set; }
        public required string UserPwd { get; set; }
        public required string UserName { get; set; }
        public required int Client_UDP_Port { get; set; }
    }

    [MemoryPackable]
    public partial class GCSPack_LoginResponse : GCSPack, IGCSResponsePack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required int ServerUDP_Port { get; set; }
        public required bool Success { get; set; }
        public required string ResponseTo { get; set; }
    }

    [MemoryPackable]
    public partial class GCSPack_LoginHint : GCSPack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
    }
}
