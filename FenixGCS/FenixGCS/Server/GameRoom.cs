using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FenixGCSApi.Server
{
    public class GameRoom
    {
        public string RoomID { get; set; }
        public ConcurrentDictionary<string, ClientEntity> Clients { get; set; } = new ConcurrentDictionary<string, ClientEntity>();
        public ConcurrentDictionary<string, int> ClientNumber { get; set; } = new ConcurrentDictionary<string, int>();
        public ClientEntity Host { get; set; }

        
    }
}
