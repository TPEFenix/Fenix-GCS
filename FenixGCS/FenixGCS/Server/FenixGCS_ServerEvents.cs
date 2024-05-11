using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FenixGCSApi.Server
{
    public delegate ActionResult<bool> LoginProcess(string id, string pwd);
    public delegate void TCPClientConnectedEvent(TcpClient client);

    public class FenixGCS_ServerEvents
    {
        public LoginProcess LoginProcess = null;
        public GameRoomMemberModifyEvent GameRoomJoin;
        public GameRoomMemberModifyEvent GameRoomLeave;
        public TCPClientConnectedEvent TCPClientConnected;
    }
}
