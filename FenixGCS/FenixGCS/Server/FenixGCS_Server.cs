using System;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using MessagePack;

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
        private TcpListener _connectListener;

        private readonly object _loginLocker = new object(); 
        public List<ClientEntity> _connectingClient = new List<ClientEntity>();
        public Dictionary<string, ClientEntity> _connectedClient = new Dictionary<string, ClientEntity>();

        public void Start(IPEndPoint listenerIPEndPoint)
        {
            ListenerCancellationToken = new CancellationToken();
            Task.Run(() => { ProcessConnectListener(listenerIPEndPoint, ListenerCancellationToken); }, ListenerCancellationToken);
        }

        private void ProcessConnectListener(IPEndPoint iPEndPoint, CancellationToken token)
        {
            _connectListener = new TcpListener(iPEndPoint);

            _connectListener.Start();
            this.InfoLog("開啟監聽");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = _connectListener.AcceptTcpClient();
                    this.InfoLog("接收連入，外拋事件");
                    ClientEntity entity = new ClientEntity(client);
                    lock (_loginLocker)
                        _connectingClient.Add(entity);
                    entity.OnClientReceive += Entity_OnClientReceive;
                    entity.StartListen();
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
            GCSCommandPack pack = MessagePackSerializer.Deserialize<GCSCommandPack>(data);
            #region 登入請求
            if (pack .EMsgType == EMsgType.Login&& entity.Logged == false && _connectingClient.Contains(entity))
            {
                GCSCommand_Login_Request recvData = (GCSCommand_Login_Request)pack;

                //ForTest
                LoginProcess = (u, p) => { return true; };
                if (data == null)
                    return;

                bool success = LoginProcess.Invoke(recvData.UserID, recvData.UserPwd);
                GCSCommandPack loginRtn = new GCSCommand_Login_Response(UDP_Port, success, recvData.ID);
                var rtn = MessagePackSerializer.Serialize(loginRtn);
                entity.Send(rtn);
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
