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
    public partial class GCSPack { }
    #endregion

    [MemoryPackable]
    public partial class GCSPack_SignUpRequest : GCSPack, IGCSRequestPack
    {
        public override ESendTunnelType SendTunnelType { get; set; } = ESendTunnelType.TCP;
        public required string UserID { get; set; }
        public required string UserPwd { get; set; }
    }
}