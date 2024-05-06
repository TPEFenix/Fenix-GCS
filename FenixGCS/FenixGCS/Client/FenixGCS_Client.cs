using System;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net.Http;
using System.Text;
using FenixGCSApi.Tool;
using System.Collections.Generic;
using System.Collections.Concurrent;
using FenixGCSApi.ByteFormatter;
using FenixGCSApi.ConstantsLib;
using MemoryPack;

namespace FenixGCSApi.Client
{
    public enum ESendTunnelType { TCP, UDP }

    public enum EClientState
    {
        /// <summary>
        ///  剛建立
        /// </summary>
        Init,
        /// <summary>
        /// 與伺服器連線中
        /// </summary>
        Connecting,
        /// <summary>
        /// 與伺服器連線完成
        /// </summary>
        Connected,
        /// <summary>
        /// 與伺服器連線失敗
        /// </summary>
        ConnectFailed,
        /// <summary>
        /// 在大廳中
        /// </summary>
        Lobby,
        /// <summary>
        /// 在房間中
        /// </summary>
        InRoom
    }

    public class FenixGCS_Client : ILogable
    {
        public string UserID { get; set; }

        /// <summary>
        /// Log紀錄事件
        /// </summary>
        public LogEvent OnLog { get; set; }

        public EClientState EClientState { get; set; }

        private ConcurrentDictionary<string, ManualResetEvent> _sendingRequestHooks = new ConcurrentDictionary<string, ManualResetEvent>();
        private ConcurrentDictionary<string, byte[]> _responseCollection = new ConcurrentDictionary<string, byte[]>();
        private ManualResetEvent _pleaseLogin = new ManualResetEvent(false);

        private TcpClient _tcpClient;
        private UdpClient _udpClient;


        private FGCSByteFormatter _tcpByteFormatter = new FGCSByteFormatter();
        private FGCSByteFormatter _udpByteFormatter = new FGCSByteFormatter();

        private CancellationTokenSource _tcpListenCancelTokenSource;
        private CancellationTokenSource _udpListenCancelTokenSource;

        private CancellationTokenSource _tcpFormatterCancelTokenSource;
        private CancellationTokenSource _udpFormatterCancelTokenSource;

        private IPEndPoint _serverUDPEndPoint;
        private IPEndPoint MyRemoteUDPEndPoint;

        private KeepJobQueue<byte[]> _tcpSendJobQueue;
        private KeepJobQueue<byte[]> _udpSendJobQueue;
        private KeepJobQueue<byte[]> _receiveJobQueue;

        public FenixGCS_Client()
        {
            EClientState = EClientState.Init;
            _receiveJobQueue = new KeepJobQueue<byte[]>(ReceiveData);
        }


        /// <summary>
        /// 登入到遊戲伺服器
        /// </summary>
        /// <param name="serverListenIP">登入監聽IPEndPoint</param>
        /// <param name="udpListenPoint"></param>
        /// <param name="userID"></param>
        /// <param name="userPwd"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        public bool ConnectToServer(IPEndPoint serverListenIP, IPEndPoint udpListenPoint, string userID, string userPwd, string userName)
        {
            UserID = userID;
            _pleaseLogin = new ManualResetEvent(false);
            EClientState = EClientState.Connecting;

            _tcpClient = new TcpClient(IPPortFinder.FindAvailableTcpEndpoint());
            _tcpClient.Connect(serverListenIP);

            StartRecvFromTCPFormatter();
            StartListenFromTCPThread();
            _tcpSendJobQueue = new KeepJobQueue<byte[]>(SendByTCP);
            _udpSendJobQueue = new KeepJobQueue<byte[]>(SendByUDP);

            if (!_pleaseLogin.WaitOne(5000))//等待伺服器說明可以登入
                return false;
            _pleaseLogin.Reset();

            //查詢UDP
            _udpClient = new UdpClient(0);
            _udpClient.Connect(udpListenPoint);

            byte[] udpRemoteRtn = null;
            bool udpDataGetFailed = false;
            Task recvTask = Task.Run(() =>
            {
                try
                {
                    IPEndPoint remote = null;
                    udpRemoteRtn = _udpClient.Receive(ref remote);
                }
                catch (Exception e)
                {
                    OnLog?.Invoke(ELogLevel.Error, "獲取UDP外部訊息失敗:" + e.Message);
                    udpDataGetFailed = true;
                }
            });

            _udpClient.Send(Constants.CheckUDPRemotePoint, Constants.CheckUDPRemotePoint.Length);
            if (!recvTask.Wait(3000) || udpDataGetFailed)
                return false;//無法取得UDPPort

            //得到自己的UDP遠端IP，要傳送給Server讓Server認識
            IPEndPoint udpInfo = MemoryPackSerializer.Deserialize<IPEndPointStruct>(udpRemoteRtn);
            MyRemoteUDPEndPoint = udpInfo;
            var rtnData = ServerLogin(userID, userPwd, userName, 5000);
            if (rtnData.Success)
            {
                if (rtnData.Result is GCSPack_LoginResponse)
                {
                    if (rtnData.Result.Success)
                    {
                        EClientState = EClientState.Connected;
                        _serverUDPEndPoint = new IPEndPoint(serverListenIP.Address, rtnData.Result.ServerUDP_Port);
                        StartRecvFromUDPFormatter();
                        StartListenFromUDPThread();
                        return true;
                    }
                    else
                    {
                        EClientState = EClientState.ConnectFailed;
                        this.ErrorLog("登入失敗");
                    }
                }
            }
            else
            {
                this.ErrorLog(rtnData.Message);
            }

            //後續應加強使用時間標記的交替加密方式
            //Server所存的資料是使用者帳號明文與使用者密碼經過MD5後的密文
            //流程
            //User傳送使用者帳號->Server回傳使用User帳號找到的Md5後的密碼(以下簡稱MD5Pwd)，把當下時間作為"回傳時間(以下簡稱Time)"以MD5P進行加密(並記住回傳時間)
            //->User使用Md5後的密碼解密得到"Time"->使用者把原始密碼進行Md5後再用"Time"進行AES加密(以下簡稱final)->
            //Server接收到final，以"Time"AES解密並核對資料是否符合資料庫所儲存的MD5Pwd

            return false;
        }

        /// <summary>
        /// 直接傳送資料給Server(建議還是使用特定的指令函式)
        /// </summary>
        public void SendBinaryToServer(byte[] data, ESendTunnelType type)
        {
            if (type == ESendTunnelType.TCP)
                _tcpSendJobQueue.Enqueue(data);
            else if (type == ESendTunnelType.UDP)
                _udpSendJobQueue.Enqueue(data);
        }
        public IGCSResponsePack SendRequestPackToServer(IGCSRequestPack request, ESendTunnelType type, int timeout = Timeout.Infinite)
        {
            var id = request.PackID;
            _sendingRequestHooks[id] = new ManualResetEvent(false);

            byte[] serialized = request.Serialize();
            SendBinaryToServer(serialized, type);

            if (!_sendingRequestHooks[id].WaitOne(timeout))
                throw new TimeoutException("Timeout");

            _sendingRequestHooks.TryRemove(id, out ManualResetEvent manualResetEvent);
            if (!_responseCollection.TryRemove(id, out byte[] rtn))
                throw new Exception("Can't find Rtn");

            return (IGCSResponsePack)GCSPack.Deserialize<GCSPack>(rtn);
        }
        public void SendPackToServer(GCSPack pack)
        {
            pack.SenderID = UserID;
            var serialized = pack.Serialize();
            SendBinaryToServer(serialized, pack.SendTunnelType);
        }


        private void SendByTCP(byte[] data)
        {
            _tcpClient.Client.Send(FGCSByteFormatter.GenerateSendArray(data));
        }
        private void SendByUDP(byte[] data)
        {
            var sendData = FGCSByteFormatter.GenerateSendArray(data);
            _udpClient.Send(sendData, sendData.Length);
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
        private void StartListenFromUDPThread()
        {
            if (_udpListenCancelTokenSource != null)
                _udpListenCancelTokenSource.Cancel();
            _udpListenCancelTokenSource = new CancellationTokenSource();
            Task.Run(() =>
            {
                while (!_udpListenCancelTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        IPEndPoint recvIP = null;
                        byte[] data = _udpClient.Receive(ref recvIP);
                        if (!recvIP.Equals(_serverUDPEndPoint))
                            continue;
                        _udpByteFormatter.InsertSourceData(data);
                    }
                    catch (Exception e) { Console.WriteLine("去你得"); }
                }
            });
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
            var data = GCSPack.Deserialize<GCSPack>(recv);
            #region 處理錯誤類別
            if (data == null)
            {
                OnLog?.Invoke(ELogLevel.Error, "ErrorData");
                return;
            }
            #endregion

            #region 處理Response
            if (data is IGCSResponsePack)
            {
                IGCSResponsePack request = (IGCSResponsePack)data;
                if (_sendingRequestHooks.TryGetValue(request.ResponseTo, out ManualResetEvent manualResetEvent))
                {
                    _responseCollection.TryAdd(request.ResponseTo, recv);
                    manualResetEvent.Set();
                }
                return;
            }
            #endregion

            if (data is GCSPack_LoginHint)
            {
                _pleaseLogin.Set();
            }
        }

        #region 溝通
        public ActionResult<GCSPack_LoginResponse> ServerLogin(string userID, string userPwd, string userName, int timeout = Timeout.Infinite)
        {
            GCSPack_LoginRequest data = new GCSPack_LoginRequest()
            {
                Client_UDP_Info = MyRemoteUDPEndPoint,
                SenderID = userID,
                UserID = userID,
                UserName = userName,
                UserPwd = userPwd,
            };
            try
            {
                var obj = SendRequestPackToServer(data, ESendTunnelType.TCP, timeout);
                ActionResult<GCSPack_LoginResponse> rtn = new ActionResult<GCSPack_LoginResponse>(true, obj as GCSPack_LoginResponse);
                return rtn;
            }
            catch (TimeoutException)
            {
                return new ActionResult<GCSPack_LoginResponse>(false, null, "Timeout");
            }
            catch (Exception e)
            {
                return new ActionResult<GCSPack_LoginResponse>(false, null, e.Message);
            }
        }
        #endregion
    }
}
