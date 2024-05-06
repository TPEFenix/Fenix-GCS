using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FenixGCSApi.Server
{
    public class ClientManager
    {

        private readonly object _lock = new object();
        private List< ClientEntity> _connectingClient { get; set; } = new List< ClientEntity>();

        //會在以下三個的都是已登入成功的
        private ConcurrentDictionary<string, ClientEntity> _dicByUserID = new ConcurrentDictionary<string, ClientEntity>();
        private ConcurrentDictionary<IPEndPointStruct, ClientEntity> _dicByTCPInfo = new ConcurrentDictionary<IPEndPointStruct, ClientEntity>();
        private ConcurrentDictionary<IPEndPointStruct, ClientEntity> _dicByUDPInfo = new ConcurrentDictionary<IPEndPointStruct, ClientEntity>();

        public void LoginUser(ClientEntity client)
        {
            lock (_lock)
            {
                if (!_connectingClient.Contains(client))
                    return;//失敗
                _connectingClient.Remove(client);
                if (_dicByUserID.ContainsKey(client.USER_ID))//剔退
                    _dicByUserID[client.USER_ID].ProcessKickout();
            }
            _dicByUserID[client.USER_ID] = client;
            _dicByTCPInfo[client.RemoteTCPEndPoint] = client;
            _dicByUDPInfo[client.RemoteUDPEndPoint] = client;
        }
        public void SetClientConnecting(ClientEntity client)
        {
            lock (_lock)
            {
                if (!_connectingClient.Contains(client))
                    _connectingClient.Add(client);
            }
        }
        public void RemoveClientFromConnectingList(ClientEntity client)
        {
            lock (_lock)
            {
                if (!_connectingClient.Contains(client))
                    return;//失敗
                _connectingClient.Remove(client);
            }
        }

        public ClientEntity FindClientByID(string ID)
        {
            _dicByUserID.TryGetValue(ID, out ClientEntity returner);
            return returner;
        }
        public ClientEntity FindClientByTCPInfo(IPEndPointStruct info)
        {
            _dicByTCPInfo.TryGetValue(info, out ClientEntity returner);
            return returner;
        }
        public ClientEntity FindClientByUDPInfo(IPEndPointStruct info)
        {
            _dicByUDPInfo.TryGetValue(info, out ClientEntity returner);
            return returner;
        }

        public bool IsEntityConnecting(ClientEntity entity)
        {
            return _connectingClient.Contains(entity);
        }

    }
}
