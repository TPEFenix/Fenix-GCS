using MessagePack;

namespace FenixGCSApi
{
    [MessagePackObject(true)]
    public partial class GCSCommand_Login_Request : GCSCommandPack
    {
        public string UserID { get; set; }
        public string UserPwd { get; set; }
        public string UserName { get; set; }
        public GCSCommand_Login_Request(string userID, string userPwd, string UserName) : base(EMsgType.Login, true)
        {
            UserID = userID;
            UserPwd = userPwd;
            this.UserName = UserName;
        }
    }
    [MessagePackObject(true)]
    public partial class GCSCommand_Login_Response : GCSCommandPack
    {
        public bool Success { get; set; }
        public int UDP_Port { get; set; }
        public GCSCommand_Login_Response(int udp_Port, bool success, string responseTo) : base(EMsgType.LoginRtn, false, responseTo)
        {
            Success = success;
            UDP_Port = udp_Port;
        }
    }
}
