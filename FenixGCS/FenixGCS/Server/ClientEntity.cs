using FenixGCSApi.ByteFormatter;
using FenixGCSApi.Client;
using FenixGCSApi.Tool;
using MemoryPack;
using System;
using System.Collections.Generic;
using System.Net;
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

        private TcpClient _tcpClient;
        private UdpClient _serverUDP;

        private FGCSByteFormatter _tcpByteFormatter = new FGCSByteFormatter();
        private FGCSByteFormatter _udpByteFormatter = new FGCSByteFormatter();

        private CancellationTokenSource _tcpListenCancelTokenSource;

        private CancellationTokenSource _tcpFormatterCancelTokenSource;
        private CancellationTokenSource _udpFormatterCancelTokenSource;

        public IPEndPoint ServerUDPEndPoint => (IPEndPoint)_serverUDP.Client.LocalEndPoint;

        private KeepJobQueue<byte[]> _tcpSendJobQueue;
        private KeepJobQueue<byte[]> _udpSendJobQueue;
        private KeepJobQueue<byte[]> _receiveJobQueue;

        public IPAddress RemoteIP => ((IPEndPoint)_tcpClient.Client.RemoteEndPoint).Address;

        public IPEndPoint RemoteTCPEndPoint => ((IPEndPoint)_tcpClient.Client.RemoteEndPoint);
        public IPEndPoint RemoteUDPEndPoint;

        /// <summary>
        ///  這個ClientEntity所在的GameRoom
        /// </summary>
        public GameRoom InRoom { get; set; }

        public string USER_ID { get; set; }
        public string USER_NAME { get; set; }

        public ClientEntity(TcpClient tcpClient, UdpClient serverUDP)
        {
            _tcpClient = tcpClient;
            _serverUDP = serverUDP;
            _receiveJobQueue = new KeepJobQueue<byte[]>(ReceiveData);
            _tcpSendJobQueue = new KeepJobQueue<byte[]>(SendByTCP);
            _udpSendJobQueue = new KeepJobQueue<byte[]>(SendByUDP);
        }

        public void StartListen()
        {
            StartRecvFromUDPFormatter();
            StartRecvFromTCPFormatter();
            StartListenFromTCPThread();
        }
        private void SendByTCP(byte[] data)
        {
            _tcpClient.Client.Send(FGCSByteFormatter.GenerateSendArray(data));
        }
        private void SendByUDP(byte[] data)
        {
            var sendData = FGCSByteFormatter.GenerateSendArray(data);
            _serverUDP.Send(sendData, sendData.Length, RemoteUDPEndPoint);
        }

        /// <summary>
        /// 直接傳送資料給Server(建議還是使用特定的指令函式)
        /// </summary>
        public void SendBinaryToTarget(byte[] data, ESendTunnelType type)
        {
            if (type == ESendTunnelType.TCP)
                _tcpSendJobQueue.Enqueue(data);
            else if (type == ESendTunnelType.UDP)
                _udpSendJobQueue.Enqueue(data);
        }

        /// <summary>
        /// 直接傳送資料給Server(送出Pack)
        /// </summary>
        public void SendPackToTarget(GCSPack pack)
        {
            SendBinaryToTarget(pack.Serialize(), pack.SendTunnelType);
        }

        private void StartListenFromTCPThread()
        {
            if (_tcpListenCancelTokenSource != null)
                _tcpListenCancelTokenSource.Cancel();
            _tcpListenCancelTokenSource = new CancellationTokenSource();

            Task.Run(() =>
            {
                while (!_tcpListenCancelTokenSource.IsCancellationRequested)
                {
                    byte[] data = new byte[1024];
                    int length = _tcpClient.Client.Receive(data);
                    _tcpByteFormatter.InsertSourceData(data);
                }
            });

        }
        public void InsertDataFromUDP(byte[] data)
        {
            _udpByteFormatter.InsertSourceData(data);
        }

        private void StartRecvFromTCPFormatter()
        {
            if (_tcpFormatterCancelTokenSource != null)
                _tcpFormatterCancelTokenSource.Cancel();
            _tcpFormatterCancelTokenSource = new CancellationTokenSource();
            Task.Run(() =>
            {
                while (!_tcpFormatterCancelTokenSource.IsCancellationRequested)
                {
                    byte[] data = _tcpByteFormatter.Receive();
                    _receiveJobQueue.Enqueue(data);
                }
            });
        }
        private void StartRecvFromUDPFormatter()
        {
            if (_udpFormatterCancelTokenSource != null)
                _udpFormatterCancelTokenSource.Cancel();
            _udpFormatterCancelTokenSource = new CancellationTokenSource();
            Task.Run(() =>
            {
                while (!_udpFormatterCancelTokenSource.IsCancellationRequested)
                {
                    byte[] data = _udpByteFormatter.Receive();
                    _receiveJobQueue.Enqueue(data);
                }
            });
        }

        private void ReceiveData(byte[] recv)
        {
            //處理Response
            OnClientReceive?.Invoke(this, recv);
        }
        public void ProcessKickout()
        {

        }
    }
}
