using FenixGCSApi.Client;
using MemoryPack;
using System.Net;

namespace FenixGCSApi
{
    [MemoryPackable]
    public partial class GCSCommand_Login_Request : GCSCommandPack
    {
        public string UserID { get; set; }
        public string UserPwd { get; set; }
        public string UserName { get; set; }
        public int Client_UDP_Port { get; set; }

        public GCSCommand_Login_Request(string userID, string userPwd, string userName, int client_UDP_Port, ESendTunnelType tunnelType = ESendTunnelType.TCP) : base(EMsgType.Login, true, null, tunnelType)
        {
            UserID = userID;
            UserPwd = userPwd;
            UserName = userName;
            Client_UDP_Port = client_UDP_Port;
        }
    }
    [MemoryPackable]
    public partial class GCSCommand_Login_Response : GCSCommandPack
    {
        public bool Success { get; set; }
        public int ServerUDP_Port { get; set; }
        public GCSCommand_Login_Response(int serverudp_Port, bool success, string responseTo, ESendTunnelType tunnelType = ESendTunnelType.TCP) : base(EMsgType.LoginRtn, false, responseTo, tunnelType)
        {
            Success = success;
            ServerUDP_Port = serverudp_Port;
        }
    }
}
