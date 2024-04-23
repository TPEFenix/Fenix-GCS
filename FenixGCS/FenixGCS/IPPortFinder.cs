using System.Net.Sockets;
using System.Net;

namespace FenixGCSApi
{
    public static class IPPortFinder
    {
        public static IPEndPoint FindAvailableUdpEndpoint()
        {
            using (Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                udpSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                IPEndPoint localEndPoint = udpSocket.LocalEndPoint as IPEndPoint;
                return localEndPoint;
            }
        }
        public static IPEndPoint FindAvailableTcpEndpoint()
        {
            using (Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                tcpSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                IPEndPoint localEndPoint = tcpSocket.LocalEndPoint as IPEndPoint;
                return localEndPoint;
            }
        }
    }
}
