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

        private List< ClientEntity> _connectingClient { get; set; } = new List< ClientEntity>();

        //會在以下三個的都是已登入成功的
        private ConcurrentDictionary<string, ClientEntity> _dicByUserID = new ConcurrentDictionary<string, ClientEntity>();
        private ConcurrentDictionary<IPEndPointStruct, ClientEntity> _dicByTCPInfo = new ConcurrentDictionary<IPEndPointStruct, ClientEntity>();
        private ConcurrentDictionary<IPEndPointStruct, ClientEntity> _dicByUDPInfo = new ConcurrentDictionary<IPEndPointStruct, ClientEntity>();

        public void LoginUser(ClientEntity client)
        {
            if (!_connectingClient.Contains(client))
                return;//失敗
            _connectingClient.Remove(client);
            if (_dicByUserID.ContainsKey(client.USER_ID))//剔退
                _dicByUserID[client.USER_ID].ProcessKickout();
            _dicByUserID[client.USER_ID] = client;
            _dicByTCPInfo[client.RemoteTCPEndPoint] = client;
            _dicByUDPInfo[client.RemoteUDPEndPoint] = client;
        }
        public void SetClientConnecting(ClientEntity client)
        {
            if (!_connectingClient.Contains(client))
            {
                _connectingClient.Add(client);
            }
        }

        public ClientEntity FindClientByID(string ID)
        {
            return _dicByUserID[ID];
        }
        public ClientEntity FindClientByTCPInfo(IPEndPointStruct info)
        {
            return _dicByTCPInfo[info];
        }
        public ClientEntity FindClientByUDPInfo(IPEndPointStruct info)
        {
            return _dicByUDPInfo[info];
        }

    }
}
