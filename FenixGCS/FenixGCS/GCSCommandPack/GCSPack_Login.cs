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
        public string UserID { get; set; }
        public string UserPwd { get; set; }
        public string UserName { get; set; }
        public int Client_UDP_Port { get; set; }
    }

    [MemoryPackable]
    public partial class GCSPack_LoginResponse : GCSPack, IGCSResponsePack
    {
        public int ServerUDP_Port { get; set; }
        public bool Success { get; set; }
        public string ResponseTo { get; set; }
    }

    [MemoryPackable]
    public partial class GCSPack_LoginHint : GCSPack
    {

    }
}
