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
        private IPEndPoint localUDPEndPoint => (IPEndPoint)_udpClient.Client.LocalEndPoint;

        private KeepJobQueue<byte[]> _tcpSendJobQueue;
        private KeepJobQueue<byte[]> _udpSendJobQueue;
        private KeepJobQueue<byte[]> _receiveJobQueue;

        public bool IsPleaseLoginRecv(byte[] recv)
        {
            bool success = true;
            if (recv.Length == 3)
            {
                for (int i = 0; i < recv.Length; i++)
                {
                    if (Constants.PleaseLogin[i] != recv[i])
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

        public FenixGCS_Client()
        {
            EClientState = EClientState.Init;
            _receiveJobQueue = new KeepJobQueue<byte[]>(ReceiveData);
        }

        public bool ConnectToServer(IPEndPoint serverListenIP, string userID, string userPwd, string userName)
        {
            _pleaseLogin = new ManualResetEvent(false);
            EClientState = EClientState.Connecting;

            _tcpClient = new TcpClient(IPAddress.Any.AddressFamily);
            _tcpClient.Connect(serverListenIP);

            StartRecvFromTCPFormatter();
            StartListenFromTCPThread();
            _udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            _tcpSendJobQueue = new KeepJobQueue<byte[]>(SendByTCP);
            _udpSendJobQueue = new KeepJobQueue<byte[]>(SendByUDP);

            if (!_pleaseLogin.WaitOne(5000))//等待伺服器說明可以登入
                return false;
            _pleaseLogin.Reset();

            var rtnData = ServerLogin(userID, userPwd, userName, 5000);
            if (rtnData.Success)
            {
                if (rtnData.Result.EMsgType == EMsgType.LoginRtn)
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
        public GCSCommandPack SendRequestPackToServer(GCSCommandPack request, ESendTunnelType type, int timeout = Timeout.Infinite)
        {
            var id = request.ID;
            if (!request.IsRequest)
                throw new Exception("this Package is not request");
            _sendingRequestHooks[id] = new ManualResetEvent(false);

            var serialized = request.Serialize();
            SendBinaryToServer(serialized, type);

            if (!_sendingRequestHooks[id].WaitOne(timeout))
                throw new TimeoutException("Timeout");

            _sendingRequestHooks.TryRemove(id, out ManualResetEvent manualResetEvent);
            if (!_responseCollection.TryRemove(id, out byte[] rtn))
                throw new Exception("Can't find Rtn");

            return GCSCommandPack.Deserialize(rtn);
        }
        public void SendPackToServer(GCSCommandPack pack)
        {
            var serialized = pack.Serialize();
            SendBinaryToServer(serialized, pack.TunnelType);
        }


        private void SendByTCP(byte[] data)
        {
            _tcpClient.Client.Send(FGCSByteFormatter.GenerateSendArray(data));
        }
        private void SendByUDP(byte[] data)
        {
            var sendData = FGCSByteFormatter.GenerateSendArray(data);
            _udpClient.Send(sendData, sendData.Length, _serverUDPEndPoint);
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
                    IPEndPoint recvIP = null;
                    byte[] data = _udpClient.Receive(ref recvIP);
                    if (!recvIP.Equals(_serverUDPEndPoint))
                        continue;
                    _udpByteFormatter.InsertSourceData(data);
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
            //處理Response
            GCSCommandPack data = GCSCommandPack.Deserialize(recv);

            if (!string.IsNullOrEmpty(data.ResponseTo))
            {
                if (_sendingRequestHooks.TryGetValue(data.ResponseTo, out ManualResetEvent manualResetEvent))
                {
                    _responseCollection.TryAdd(data.ResponseTo, recv);
                    manualResetEvent.Set();
                }
                return;
            }
            else//基本指令
            {
                if (data.EMsgType == EMsgType.LoginHint)
                {
                    _pleaseLogin.Set();
                }
            }
        }

        #region 溝通
        public ActionResult<GCSCommand_Login_Response> ServerLogin(string userID, string userPwd, string userName, int timeout = Timeout.Infinite)
        {
            GCSCommand_Login_Request data = new GCSCommand_Login_Request(userID, userPwd, userName, localUDPEndPoint.Port);
            try
            {
                var obj = SendRequestPackToServer(data, ESendTunnelType.TCP, timeout);
                ActionResult<GCSCommand_Login_Response> rtn = new ActionResult<GCSCommand_Login_Response>(true, obj as GCSCommand_Login_Response);
                return rtn;
            }
            catch (TimeoutException)
            {
                return new ActionResult<GCSCommand_Login_Response>(false, null, "Timeout");
            }
            catch (Exception e)
            {
                return new ActionResult<GCSCommand_Login_Response>(false, null, e.Message);
            }
        }
        #endregion
    }
}
