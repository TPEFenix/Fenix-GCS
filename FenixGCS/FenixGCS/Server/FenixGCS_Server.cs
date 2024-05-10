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
using System.IO;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.Concurrent;


namespace FenixGCSApi.Server
{
    public delegate ActionResult<bool> LoginProcess(string id, string pwd);

    public delegate void TCPClientConnectedEvent(TcpClient client);

    public class FenixGCS_Server : ILogable
    {
        //Event
        public LoginProcess LoginProcess = null;
        public LogEvent OnLog { get; set; }
        public GameRoomMemberModifyEvent GameRoomJoin;
        public GameRoomMemberModifyEvent GameRoomLeave;

        public static readonly string SERVERSENDERID = (char)6 + "#ServerID" + (char)6;
        public Encoding Encoding = Encoding.UTF8;
        public TCPClientConnectedEvent TCPClientConnected;
        public CancellationToken ListenerCancellationToken { get; set; } = new CancellationToken();
        public CancellationToken DefaultSignUpCancellationToken { get; set; } = new CancellationToken();
        private TcpListener _tcpListener;
        private UdpClient _udpClient;
        private CancellationTokenSource _udpListenCancelTokenSource;

        public ConcurrentDictionary<string, GameRoom> GameRooms = new ConcurrentDictionary<string, GameRoom>();

        public ClientManager ClientManager { get; set; } = new ClientManager();

        public IPEndPoint TCPIPEndPoint { private set; get; }
        public int TCP_Port => TCPIPEndPoint.Port;
        public IPEndPoint UDPIPEndPoint { private set; get; }
        public int UDP_Port => UDPIPEndPoint.Port;


        public void Start(IPEndPoint tcpIPEndPoint, IPEndPoint udpIPEndPoint)
        {
            TCPIPEndPoint = tcpIPEndPoint;
            UDPIPEndPoint = udpIPEndPoint;
            _udpClient = new UdpClient(UDPIPEndPoint);
            _tcpListener = new TcpListener(TCPIPEndPoint);
            ListenerCancellationToken = new CancellationToken();
            StartListenFromUDPThread();
            Task.Run(() => { ProcessConnectListener(ListenerCancellationToken); }, ListenerCancellationToken);
        }

        /// <summary>
        /// 啟動預設的註冊程式，這個是直接經由TCP進行註冊，且沒有加密，請慎用
        /// (這個Function僅供測試，將開啟TCP預設使用者註冊方式，沒有任何防護措施，您應該建立自己的LoginProcess與註冊管道)
        /// </summary>
        /// <param name="tcpIPEndPoint"></param>
        [Obsolete("這個Function僅供測試，將開啟TCP預設使用者註冊方式，沒有任何防護措施，您應該建立自己的LoginProcess與註冊管道")]
        public void StartDefaultSignUpProcess(IPEndPoint tcpIPEndPoint)
        {
            TcpListener signupListener = new TcpListener(tcpIPEndPoint);
            signupListener.Start();
            this.InfoLog("開啟預設註冊監聽");
            Task.Run(() =>
            {
                while (!DefaultSignUpCancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        TcpClient client = signupListener.AcceptTcpClient();
                        Task.Run(() =>
                        {
                            byte[] bytes = new byte[1024];
                            //接受註冊
                            client.Client.Receive(bytes);
                            GCSPack pack = GCSPack.Deserialize<GCSPack>(bytes);
                            if (pack == null)
                                return;
                            #region 註冊請求

                            if (pack is GCSPack_SignUpRequest registerRequest)
                            {
                                ActionResult<bool> actionResult;
                                actionResult = DefaultRegisterProcess(registerRequest.UserID, registerRequest.UserPwd);
                                if (actionResult != null)
                                {
                                    if (actionResult.Success)
                                    {
                                        GCSPack_SignUpResponse response = new GCSPack_SignUpResponse()
                                        {
                                            Success = true,
                                            ResponseTo = registerRequest.PackID,
                                            ResponseMsg = $"註冊成功",
                                            SenderID = SERVERSENDERID,
                                        };
                                        client.Client.Send(response.Serialize());
                                    }
                                    else
                                    {
                                        GCSPack_SignUpResponse response = new GCSPack_SignUpResponse()
                                        {
                                            Success = false,
                                            ResponseTo = registerRequest.PackID,
                                            ResponseMsg = $"註冊失敗:{actionResult.Message}",
                                            SenderID = SERVERSENDERID,
                                        };
                                        client.Client.Send(response.Serialize());
                                    }
                                }
                                else
                                {
                                    GCSPack_SignUpResponse response = new GCSPack_SignUpResponse()
                                    {
                                        Success = false,
                                        ResponseTo = registerRequest.PackID,
                                        ResponseMsg = "註冊未獲得回傳",
                                        SenderID = SERVERSENDERID,
                                    };
                                    client.Client.Send(response.Serialize());
                                }
                            }

                            #endregion


                        });
                    }
                    catch (Exception ex)
                    {
                        this.ErrorLog("(DefaultSignup)TCP Listener exception: " + ex.Message);
                    }
                }
                if (DefaultSignUpCancellationToken.IsCancellationRequested)
                {

                }
            });
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
            if (pack is GCSPack_LoginRequest loginRequest)
            {
                if (entity.Logged || !ClientManager.IsEntityConnecting(entity))
                    return;//不可重複登入或沒有先進行TCP連線

                ActionResult<bool> actionResult;
                if (LoginProcess != null)
                    actionResult = LoginProcess.Invoke(loginRequest.UserID, loginRequest.UserPwd);
                else
                    actionResult = new ActionResult<bool>(true, true, "登入成功");

                if (actionResult.Success)
                {
                    GCSPack_LoginResponse loginRtn = new GCSPack_LoginResponse()
                    {
                        Success = actionResult.Success,
                        SenderID = SERVERSENDERID,
                        ServerUDP_Port = UDP_Port,
                        ResponseTo = loginRequest.PackID
                    };
                    entity.USER_ID = loginRequest.UserID;
                    entity.RemoteUDPEndPoint = loginRequest.Client_UDP_Info;
                    this.DebugLog($"RemoteUDPEndPoint = {loginRequest.Client_UDP_Info.ipEndPoint}");
                    ClientManager.LoginUser(entity);
                    entity.Logged = true;
                    entity.SendBinaryToTarget(loginRtn.Serialize(), Client.ESendTunnelType.TCP);
                }
                else
                {
                    GCSPack_LoginResponse loginRtn = new GCSPack_LoginResponse()
                    {
                        Success = actionResult.Success,
                        SenderID = SERVERSENDERID,
                        ServerUDP_Port = -1,
                        ResponseTo = loginRequest.PackID
                    };
                    entity.Logged = false;
                    ClientManager.RemoveClientFromConnectingList(entity);
                    entity.SendPackToTarget(loginRtn);
                }

                this.DebugLog("LoginProcessEnd");
            }

            #endregion

            if (!entity.Logged)//需要登入後才可以用的功能
                return;


            #region 登入後請求
            if (pack is GCSPack_CreateRoomRequest createRoomRequest)
            {
                if (GameRooms.ContainsKey(createRoomRequest.RoomID))
                {
                    //創建房間失敗
                    GCSPack_CreateRoomResponse response = new GCSPack_CreateRoomResponse()
                    {
                        Success = false,
                        ResponseTo = createRoomRequest.PackID,
                        SenderID = SERVERSENDERID,
                        ResponseMsg = "創建房間失敗，已經有此房間ID",
                    };
                    entity.SendPackToTarget(response);
                }
                else
                {
                    GameRoom room = new GameRoom()
                    {
                        HostUserID = entity.USER_ID,
                        RoomID = createRoomRequest.RoomID,
                        RoomInfo = createRoomRequest.RoomInfo,
                    };
                    room.OnJoin += (r, id) => { GameRoomJoin?.Invoke(r, id); };
                    room.OnLeave += (r, id) =>
                    {
                        GameRoomLeave?.Invoke(r, id);
                        if (r.MemberIDs.Count <= 0)
                            GameRooms.Remove(r.RoomID, out GameRoom value);
                    };
                    GameRooms.TryAdd(createRoomRequest.RoomID, room);
                    room.AddUser(entity.USER_ID);
                    GCSPack_CreateRoomResponse response = new GCSPack_CreateRoomResponse()
                    {
                        Success = true,
                        ResponseTo = createRoomRequest.PackID,
                        SenderID = SERVERSENDERID,
                        ResponseMsg = "創建房間成功",
                    };
                    entity.SendPackToTarget(response);
                }
            }
            #endregion
        }

        #region 預設註冊程式

        public ActionResult<bool> DefaultRegisterProcess(string id, string pwd)
        {
            string usersDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Users");

            // 確保 Users 資料夾存在
            if (!Directory.Exists(usersDirectory))
                Directory.CreateDirectory(usersDirectory);


            string userFilePath = Path.Combine(usersDirectory, id + ".dat");

            // 檢查用戶檔案是否存在
            if (File.Exists(userFilePath))
                return new ActionResult<bool>(false, false, "用戶已經存在"); // 如果用戶已存在，則返回 False

            // 創建用戶資料並序列化
            UserDataInfo userData = new UserDataInfo
            {
                ID = id,
                Pwd = pwd,
            };

            // 將 userData 序列化到檔案中
            using (var fileStream = File.Create(userFilePath))
            {
                var bytes = MemoryPackSerializer.Serialize(userData);
                fileStream.Write(bytes);
            }

            return new ActionResult<bool>(true, true, "註冊成功"); // 如果用戶已存在，則返回 False
        }
        #endregion

    }


    [MemoryPackable]
    public partial class UserDataInfo
    {
        public string ID { get; set; }
        public string Pwd { get; set; }
    }
}
