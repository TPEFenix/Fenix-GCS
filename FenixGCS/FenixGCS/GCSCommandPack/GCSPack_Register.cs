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
    [MemoryPackUnion(20, typeof(GCSPack_SignUpRequest))]
    [MemoryPackUnion(21, typeof(GCSPack_SignUpResponse))]
    public partial class GCSPack { }
    #endregion

    [MemoryPackable]
    public partial class GCSPack_SignUpRequest : GCSPack, IGCSRequestPack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required string UserID { get; set; }
        public required string UserPwd { get; set; }
    }

    [MemoryPackable]
    public partial class GCSPack_SignUpResponse : GCSPack, IGCSResponsePack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required bool Success { get; set; }
        public required string ResponseTo { get; set; }
        public string ResponseMsg { get; set; }
    }
}