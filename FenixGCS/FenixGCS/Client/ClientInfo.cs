using MemoryPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FenixGCSApi.Client
{
    [MemoryPackable]
    public partial class ClientInfo
    {
        public string ID { get; set; }
    }
}
