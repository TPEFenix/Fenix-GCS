﻿using System;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using FenixGCSApi.ConstantsLib;


namespace FenixGCSApi.Server
{
    public delegate bool LoginProcess(string user, string encryptedPwd);
    public delegate void TCPClientConnectedEvent(TcpClient client);

    public class FenixGCS_Server : ILogable
    {
        private int UDP_Port = 30000;
        public Encoding Encoding = Encoding.UTF8;
        public LoginProcess LoginProcess;
        public LogEvent OnLog { get; set; }
        public TCPClientConnectedEvent TCPClientConnected;
        public CancellationToken ListenerCancellationToken { get; set; } = new CancellationToken();
        private TcpListener _tcpListener;
        private UdpClient _udpClient;
        private readonly object _loginLocker = new object();
        public List<ClientEntity> _connectingClient = new List<ClientEntity>();
        public Dictionary<string, ClientEntity> _connectedClient = new Dictionary<string, ClientEntity>();

        public IPEndPoint TCPIPEndPoint { private set; get; }
        public IPEndPoint UDPIPEndPoint { private set; get; }

        public void Start(IPAddress ip, int tcpPort, int udpPort)
        {
            TCPIPEndPoint = new IPEndPoint(ip, tcpPort);
            UDPIPEndPoint = new IPEndPoint(ip, udpPort);

            _udpClient = new UdpClient(UDPIPEndPoint);
            _tcpListener = new TcpListener(TCPIPEndPoint);
            ListenerCancellationToken = new CancellationToken();
            Task.Run(() => { ProcessConnectListener(ListenerCancellationToken); }, ListenerCancellationToken);
        }

        private void ProcessConnectListener(CancellationToken token)
        {
            _tcpListener.Start();
            this.InfoLog("開啟監聽");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = _tcpListener.AcceptTcpClient();
                    this.InfoLog($"TcpClientConnected IP = {((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()} , Port = {((IPEndPoint)client.Client.RemoteEndPoint).Port}");
                    ClientEntity entity = new ClientEntity(client, _udpClient);
                    lock (_loginLocker)
                        _connectingClient.Add(entity);
                    entity.OnClientReceive += Entity_OnClientReceive;
                    entity.StartListen();
                    entity.SendToTarget(Constants.PleaseLogin, Client.ESendTunnelType.TCP);
                    TCPClientConnected?.Invoke(client);
                }
                catch (Exception ex)
                {
                    this.ErrorLog("TCP Listener exception: " + ex.Message);
                }
            }
            if (token.IsCancellationRequested)
            {

            }
        }

        private void Entity_OnClientReceive(ClientEntity entity, byte[] data)
        {
            GCSCommandPack pack = GCSCommandPack.Deserialize(data);
            #region 登入請求
            if (pack.EMsgType == EMsgType.Login && entity.Logged == false && _connectingClient.Contains(entity))
            {
                GCSCommand_Login_Request recvData = (GCSCommand_Login_Request)pack;

                //ForTest
                LoginProcess = (u, p) => { return true; };
                if (data == null)
                    return;

                bool success = LoginProcess.Invoke(recvData.UserID, recvData.UserPwd);
                GCSCommandPack loginRtn = new GCSCommand_Login_Response(UDP_Port, success, recvData.ID);
                var rtn = loginRtn.Serialize();
                entity.SendToTarget(rtn, Client.ESendTunnelType.TCP);
                entity.USER_ID = recvData.UserID;
                entity.USER_NAME = recvData.UserName;
                lock (_loginLocker)
                    _connectingClient.Remove(entity);
                if (success)
                {
                    //檢查搶登
                    if (_connectedClient.ContainsKey(entity.USER_ID))
                    {
                        //剔退舊的
                        var old = _connectedClient[entity.USER_ID];
                        old.ProcessKickout();
                    }
                    _connectedClient[entity.USER_ID] = entity;
                }
            }
            #endregion
            #region 登入後請求

            #endregion
        }

    }
}
