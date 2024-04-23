using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FenixGCSApi
{
    public static class IPPortFinder
    {
        public static IPEndPoint FindAvailableUdpEndpoint()
        {
            using (Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                udpSocket.Bind(new IPEndPoint(IPAddress.Any, 0)); // 綁定到任何IP上的零端口，讓系統選擇一個端口
                IPEndPoint localEndPoint = udpSocket.LocalEndPoint as IPEndPoint;
                return localEndPoint;
            }
        }
        public static IPEndPoint FindAvailableTcpEndpoint()
        {
            using (Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                tcpSocket.Bind(new IPEndPoint(IPAddress.Any, 0)); // 綁定到任何IP上的零端口，讓系統選擇一個端口
                IPEndPoint localEndPoint = tcpSocket.LocalEndPoint as IPEndPoint;
                return localEndPoint;
            }
        }
    }
}
