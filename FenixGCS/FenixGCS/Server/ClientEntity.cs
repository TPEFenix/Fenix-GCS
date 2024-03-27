using FenixGCSApi.ByteFormatter;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FenixGCSApi.Server
{
    public delegate void ClientReceiveEvent(ClientEntity entity, byte[] data);
    public class ClientEntity
    {
        public LogEvent OnLog;
        public ClientReceiveEvent OnClientReceive;

        public bool Logged = false;
        public TcpClient TcpClient { get; set; }
        public FGCSByteFormatter Transmitter { get; set; } = new FGCSByteFormatter();

        public CancellationToken ListenerCancellationToken;

        public string USER_ID { get; set; }
        public string USER_NAME { get; set; }

        public ClientEntity(TcpClient tcpClient)
        {
            TcpClient = tcpClient;
        }
        public void StartListen()
        {
            Task.Run(() => { ProcessTCPReceive(ListenerCancellationToken); }, ListenerCancellationToken);
            Task.Run(() => { ProcessReceive(ListenerCancellationToken); }, ListenerCancellationToken);
        }
        private void ProcessTCPReceive(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                byte[] data = new byte[1024];
                TcpClient.Client.Receive(data);
                Transmitter.InsertSourceData(data);
            }
        }
        private void ProcessReceive(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var recv = Transmitter.Receive();
                OnClientReceive?.Invoke(this, recv);
            }
        }
        public void Send(byte[] data)
        {
            var array = FGCSByteFormatter.GenerateSendArray(data);
            Console.WriteLine(TcpClient.Client.Send(array));
        }
        public void ProcessKickout()
        {

        }
    }
}
