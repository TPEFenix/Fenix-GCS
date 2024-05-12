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
using System.Collections.Concurrent;


namespace FenixGCSApi.Server
{
    public class FenixGCS_Server : ILogable
    {
        public LogEvent OnLog { get; set; }

        /// <summary>
        /// 伺服器相關的外拋事件
        /// </summary>
        public FenixGCS_ServerEvents Events { get; private set; } = new FenixGCS_ServerEvents();

        /// <summary>
        /// 在整個伺服器環境中，代表由Server發出的PackSenderID
        /// </summary>
        public static readonly string SERVERSENDERID = (char)6 + "#ServerID" + (char)6;

        /// <summary>
        /// Server所使用的編碼。
        /// </summary>
        public Encoding Encoding = Encoding.UTF8;

        /// <summary>
        /// 房間列表
        /// </summary>
        public ConcurrentDictionary<string, GameRoom> GameRooms { get; private set; } = new ConcurrentDictionary<string, GameRoom>();

        /// <summary>
        ///  ClientEntity管理物件
        /// </summary>
        public ClientManager ClientManager { get; private set; } = new ClientManager();

        public IPEndPoint TCPIPEndPoint { private set; get; }
        public IPEndPoint UDPIPEndPoint { private set; get; }

        private TcpListener _tcpListener;
        private UdpClient _udpClient;
        private CancellationTokenSource _listenerCancellationToken = new CancellationTokenSource();
        private CancellationTokenSource _defaultSignUpCancellationToken = new CancellationTokenSource();
        private CancellationTokenSource _udpListenCancelTokenSource = new CancellationTokenSource();

        /// <summary>
        /// 啟動伺服器
        /// </summary>
        /// <param name="tcpIPEndPoint"></param>
        /// <param name="udpIPEndPoint"></param>
        public void Start(IPEndPoint tcpIPEndPoint, IPEndPoint udpIPEndPoint)
        {
            TCPIPEndPoint = tcpIPEndPoint;
            UDPIPEndPoint = udpIPEndPoint;

            _udpClient = new UdpClient(UDPIPEndPoint);
            _tcpListener = new TcpListener(TCPIPEndPoint);

            _listenerCancellationToken = new CancellationTokenSource();

            StartListenFromUDPThread();

            Task.Run(() => { ProcessConnectListener(_listenerCancellationToken); });
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
                while (!_defaultSignUpCancellationToken.IsCancellationRequested)
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
                                actionResult = DefaultSignUpProcess(registerRequest.UserID, registerRequest.UserPwd);
                                if (actionResult != null)
                                {
                                    if (actionResult.Success)
                                    {
                                        GCSPack_BasicResponse response = new GCSPack_BasicResponse()
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
                                        GCSPack_BasicResponse response = new GCSPack_BasicResponse()
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
                                    GCSPack_BasicResponse response = new GCSPack_BasicResponse()
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
                if (_defaultSignUpCancellationToken.IsCancellationRequested)
                {

                }
            });
        }

        /// <summary>
        /// 處理一般TCP連入監聽
        /// </summary>
        /// <param name="token"></param>
        private void ProcessConnectListener(CancellationTokenSource token)
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
                    Events.OnTCPClientConnected?.Invoke(client);
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

        /// <summary>
        /// 檢查該UDP訊息是否為特殊指令:查詢UDPIP
        /// </summary>
        /// <param name="recv"></param>
        /// <returns></returns>
        private bool IsCheckUDPPortRecv(byte[] recv)
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

        /// <summary>
        /// 開啟UDP傳輸功能
        /// </summary>
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

        /// <summary>
        /// 當有Entity收到ByteArrayData時
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="data"></param>
        private void Entity_OnClientReceive(ClientEntity entity, byte[] data)
        {
            this.DebugLog("Entity_OnClientReceive");

            GCSPack pack = GCSPack.Deserialize<GCSPack>(data);
            if (pack == null)
                return;

            #region 登入請求
            if (pack is GCSPack_LoginRequest loginRequest)
            {
                ProcessLoginUserRequest(entity, loginRequest);
                return;
            }
            #endregion

            if (!entity.Logged)//需要登入後才可以用的功能
                return;

            #region 創建房間請求
            if (pack is GCSPack_CreateRoomRequest createRoomRequest)
            {
                this.DebugLog($"創建房間:RoomID={createRoomRequest.RoomID}");
                ProcessCreateRoomRequest(entity, createRoomRequest);
                return;
            }
            #endregion

            #region 加入房間請求
            if (pack is GCSPack_JoinRoomRequest joinRoomRequest)
            {
                this.DebugLog($"嘗試加入房間:RoomID={joinRoomRequest.RoomID}");
                ProcessJoinRoomRequest(entity, joinRoomRequest);
                return;
            }
            #endregion

            //最後是預設以外的Pack將外拋給遊戲伺服器處理，而非內核
            Events.OnCustomPackRecv?.Invoke(entity, pack);
        }

        /// <summary>
        /// 對連線實體進行使用者登入
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="request"></param>
        public void ProcessLoginUserRequest(ClientEntity entity, GCSPack_LoginRequest request)
        {
            if (entity.Logged || !ClientManager.IsEntityConnecting(entity))
                return;//不可重複登入或沒有先進行TCP連線

            ActionResult<bool> actionResult;
            if (Events.LoginProcess != null)
                actionResult = Events.LoginProcess.Invoke(request.UserID, request.UserPwd);
            else
                actionResult = DefaultLoginProcess(request.UserID, request.UserPwd);

            if (actionResult.Result)
            {
                GCSPack_LoginResponse loginRtn = new GCSPack_LoginResponse()
                {
                    Success = actionResult.Result,
                    SenderID = SERVERSENDERID,
                    ServerUDP_Port = UDPIPEndPoint.Port,
                    ResponseTo = request.PackID
                };
                entity.USER_ID = request.UserID;
                entity.RemoteUDPEndPoint = request.Client_UDP_Info;
                this.DebugLog($"RemoteUDPEndPoint = {request.Client_UDP_Info.ipEndPoint}");
                ClientManager.LoginUser(entity);
                entity.Logged = true;
                entity.SendBinaryToTarget(loginRtn.Serialize(), Client.ESendTunnelType.TCP);
            }
            else
            {
                GCSPack_LoginResponse loginRtn = new GCSPack_LoginResponse()
                {
                    Success = actionResult.Result,
                    SenderID = SERVERSENDERID,
                    ServerUDP_Port = -1,
                    ResponseTo = request.PackID
                };
                entity.Logged = false;
                ClientManager.RemoveClientFromConnectingList(entity);
                entity.SendPackToTarget(loginRtn);
            }

            this.DebugLog("LoginProcessEnd");

        }

        /// <summary>
        /// 以連線實體為房主創建遊戲房間
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="request"></param>
        public void ProcessCreateRoomRequest(ClientEntity entity, GCSPack_CreateRoomRequest request)
        {
            if (GameRooms.ContainsKey(request.RoomID))
            {
                entity.SendPackToTarget(GCSPack.GenerateBasicResponse(false, request.PackID, SERVERSENDERID, "創建房間失敗，已經有此房間ID"));
            }
            else
            {
                GameRoom room = new GameRoom(request.RoomID, request.MaxMemberCount, entity.USER_ID, request.RoomInfo);

                //當使用者加入房間時外拋事件
                room.OnJoin += (r, id) =>
                { Events.OnGameRoomJoin?.Invoke(r, id); };

                //當使用者離開房間時外拋事件，此外如果是最後一個玩家離開房間則銷毀房間
                room.OnLeave += (r, id) =>
                {
                    Events.OnGameRoomLeave?.Invoke(r, id);
                    if (r.MemberIDs.Count <= 0)
                        GameRooms.Remove(r.RoomID, out GameRoom value);
                };

                //創建房間
                if (GameRooms.TryAdd(request.RoomID, room))
                {
                    room.AddUser(entity.USER_ID);
                    entity.InRoom = room;
                    entity.SendPackToTarget(GCSPack.GenerateBasicResponse(true, request.PackID, SERVERSENDERID, "創建房間成功"));
                }
            }
        }

        /// <summary>
        ///  加入房間
        /// </summary>
        public void ProcessJoinRoomRequest(ClientEntity entity, GCSPack_JoinRoomRequest request)
        {
            if (GameRooms.ContainsKey(request.RoomID))
            {
                GameRoom room = GameRooms[request.RoomID];
                if (room.AddUser(entity.USER_ID))
                {
                    entity.InRoom = room;
                    entity.SendPackToTarget(GCSPack.GenerateBasicResponse(true, request.PackID, SERVERSENDERID, "加入房間成功"));
                }
                else
                {
                    entity.SendPackToTarget(GCSPack.GenerateBasicResponse(true, request.PackID, SERVERSENDERID, "加入房間失敗，人數已滿"));
                }
            }
            else
            {
                entity.SendPackToTarget(GCSPack.GenerateBasicResponse(false, request.PackID, SERVERSENDERID, "加入房間失敗，沒有此房間ID"));
            }
        }

        /// <summary>
        /// 預設的註冊使用者程式
        /// </summary>
        /// <param name="id"></param>
        /// <param name="pwd"></param>
        /// <returns></returns>
        private ActionResult<bool> DefaultSignUpProcess(string id, string pwd)
        {
            string usersDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Users");

            // 確保 Users 資料夾存在
            if (!Directory.Exists(usersDirectory))
                Directory.CreateDirectory(usersDirectory);


            string userFilePath = Path.Combine(usersDirectory, id + ".dat");

            // 檢查用戶檔案是否存在
            if (File.Exists(userFilePath))
                return new ActionResult<bool>(true, false, "用戶已經存在"); // 如果用戶已存在，則返回 False

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

        public ActionResult<bool> DefaultLoginProcess(string id, string pwd)
        {
            string usersDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Users");
            string userFilePath = Path.Combine(usersDirectory, id + ".dat");

            // 檢查用戶檔案是否存在
            if (!File.Exists(userFilePath))
                return new ActionResult<bool>(true, false, "使用者帳密錯誤"); // 用戶檔案不存在，返回 False


            // 讀取檔案到 byte array
            byte[] fileContent;
            using (var fileStream = new FileStream(userFilePath, FileMode.Open, FileAccess.Read))
            {
                fileContent = new byte[fileStream.Length];  // 創建一個長度等於檔案大小的 byte array
                fileStream.Read(fileContent, 0, fileContent.Length);  // 讀取檔案到 byte array
            }

            // 使用 byte array 反序列化用戶資料
            UserDataInfo userData = MemoryPackSerializer.Deserialize<UserDataInfo>(fileContent);

            // 比對密碼
            if (userData.Pwd == pwd)
                return new ActionResult<bool>(true, true, "登入成功");
            else
                return new ActionResult<bool>(true, false, "使用者帳密錯誤");
        }

        /// <summary>
        /// 關閉伺服器
        /// </summary>
        public void Close()
        {
            _listenerCancellationToken.Cancel();
            _defaultSignUpCancellationToken.Cancel();
            _udpListenCancelTokenSource.Cancel();
        }
    }


    [MemoryPackable]
    public partial class UserDataInfo
    {
        public string ID { get; set; }
        public string Pwd { get; set; }
    }
}
