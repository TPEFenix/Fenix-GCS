using System;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using FenixGCSApi.ConstantsLib;
using System.Net.Http;
using MemoryPack;
using System.Collections;


namespace FenixGCSApi.Server
{
    public delegate bool LoginProcess(string user, string encryptedPwd);
    public delegate void TCPClientConnectedEvent(TcpClient client);

    public class FenixGCS_Server : ILogable
    {
        public static readonly string SERVERSENDERID = (char)6 + "#ServerID" + (char)6;
        public Encoding Encoding = Encoding.UTF8;
        public LoginProcess LoginProcess;
        public LogEvent OnLog { get; set; }
        public TCPClientConnectedEvent TCPClientConnected;
        public CancellationToken ListenerCancellationToken { get; set; } = new CancellationToken();
        private TcpListener _tcpListener;
        private UdpClient _udpClient;
        private CancellationTokenSource _udpListenCancelTokenSource;

        public ClientManager ClientManager { get; set; } = new ClientManager();

        public IPEndPoint TCPIPEndPoint { private set; get; }
        public int TCP_Port => TCPIPEndPoint.Port;
        public IPEndPoint UDPIPEndPoint { private set; get; }
        public int UDP_Port => UDPIPEndPoint.Port;


        public void Start(IPAddress ip, int tcpPort, int udpPort)
        {
            TCPIPEndPoint = new IPEndPoint(ip, tcpPort);
            UDPIPEndPoint = new IPEndPoint(ip, udpPort);
            _udpClient = new UdpClient(UDPIPEndPoint);
            _tcpListener = new TcpListener(TCPIPEndPoint);
            ListenerCancellationToken = new CancellationToken();
            StartListenFromUDPThread();
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
                    ClientManager.SetClientConnecting(entity);
                    entity.OnClientReceive += Entity_OnClientReceive;
                    entity.StartListen();
                    entity.SendPackToTarget(new GCSPack_LoginHint() { SenderID = SERVERSENDERID });
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

        public bool IsCheckUDPPortRecv(byte[] recv)
        {
            bool success = true;
            if (recv.Length == Constants.CheckUDPRemotePoint.Length)
            {
                for (int i = 0; i < recv.Length; i++)
                {
                    if (Constants.CheckUDPRemotePoint[i] != recv[i])
                    {
                        success = false;
                        continue;
                    }
                }
            }
            else
            {
                success = false;
            }
            return success;
        }

        private void StartListenFromUDPThread()
        {
            if (_udpListenCancelTokenSource != null)
                _udpListenCancelTokenSource.Cancel();
            _udpListenCancelTokenSource = new CancellationTokenSource();

            Task.Run(() =>
            {
                while (!_udpListenCancelTokenSource.IsCancellationRequested)
                {
                    IPEndPoint remoteIP = null;
                    byte[] data = _udpClient.Receive(ref remoteIP);

                    //先檢查是不是來查UDPPort的
                    if (IsCheckUDPPortRecv(data))
                    {
                        IPEndPointStruct iPEndPointStruct = remoteIP;
                        this.DebugLog($"(查詢)RemoteUDPEndPoint = {remoteIP}");
                        byte[] infoBytes = MemoryPackSerializer.Serialize(iPEndPointStruct);
                        _udpClient.SendAsync(infoBytes, infoBytes.Length, remoteIP);
                        continue;
                    }

                    this.DebugLog($"一般UDPremoteIP = {remoteIP.ToString()}");
                    ClientEntity target = ClientManager.FindClientByUDPInfo(remoteIP);
                    if (target != null)
                        target.InsertDataFromUDP(data);
                    else
                        this.DebugLog($"沒找到(target == null)");

                }
            });

        }

        private void Entity_OnClientReceive(ClientEntity entity, byte[] data)
        {
            this.DebugLog("Entity_OnClientReceive");

            GCSPack pack = GCSPack.Deserialize<GCSPack>(data);
            if (pack == null)
                return;

            #region 登入請求
            if (pack is GCSPack_LoginRequest recvData)
            {
                if (entity.Logged || !ClientManager.IsEntityConnecting(entity))
                    return;//不可重複登入或沒有先進行TCP連線

                bool success;
                if (LoginProcess != null)
                    success = LoginProcess.Invoke(recvData.UserID, recvData.UserPwd);
                else
                    success = true;

                if (success)
                {
                    GCSPack_LoginResponse loginRtn = new GCSPack_LoginResponse()
                    {
                        Success = success,
                        SenderID = SERVERSENDERID,
                        ServerUDP_Port = UDP_Port,
                        ResponseTo = recvData.PackID
                    };
                    entity.USER_ID = recvData.UserID;
                    entity.USER_NAME = recvData.UserName;
                    entity.RemoteUDPEndPoint = recvData.Client_UDP_Info;
                    this.DebugLog($"RemoteUDPEndPoint = {recvData.Client_UDP_Info.ipEndPoint}");
                    ClientManager.LoginUser(entity);
                    entity.Logged = true;
                    entity.SendBinaryToTarget(loginRtn.Serialize(), Client.ESendTunnelType.TCP);
                }
                else
                {
                    GCSPack_LoginResponse loginRtn = new GCSPack_LoginResponse()
                    {
                        Success = success,
                        SenderID = SERVERSENDERID,
                        ServerUDP_Port = -1,
                        ResponseTo = recvData.PackID
                    };
                    entity.Logged = false;
                    ClientManager.RemoveClientFromConnectingList(entity);
                    entity.SendBinaryToTarget(loginRtn.Serialize(), Client.ESendTunnelType.TCP);
                }

                this.DebugLog("LoginProcessEnd");
            }

            #endregion
            #region 登入後請求

            #endregion
        }

    }
}
