using System;
using System.Collections.Generic;
using System.Text;

namespace FenixGCSApi
{
    public enum EConnectType
    {
        TCP, UDP
    }
    public class ClientConnectEntity
    {
        public EConnectType ConnectType { get; set; }

    }
}
