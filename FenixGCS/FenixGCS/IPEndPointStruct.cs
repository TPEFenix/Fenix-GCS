using MemoryPack;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FenixGCSApi
{
    /// <summary>
    /// 給MemoryPack可以進行IPEndPoint資料傳遞的結構物件，使用時請直接轉換為IPEndPoint
    /// </summary>
    [MemoryPackable]
    public partial struct IPEndPointStruct
    {
        [MemoryPackIgnore]
        public readonly IPEndPoint ipEndPoint => (IPEndPoint)this;
        [MemoryPackIgnore]
        public readonly IPAddress IPAddress => ipEndPoint.Address;
        public byte[] AddressData { get; set; }
        public int Port { get; set; }

        public static implicit operator IPEndPoint(IPEndPointStruct str)
        {
            IPAddress address = new IPAddress(str.AddressData);
            int port = str.Port;
            return new IPEndPoint(address, port);
        }
        public static implicit operator IPEndPointStruct(IPEndPoint endPoint)
        {
            return new IPEndPointStruct() { AddressData = endPoint.Address.GetAddressBytes(), Port = endPoint.Port };
        }
        public override readonly bool Equals([NotNullWhen(true)] object obj)
        {
            if (obj is IPEndPointStruct)
            {
                IPEndPointStruct other = (IPEndPointStruct)obj;
                IPEndPoint otherPoint = other;
                return otherPoint.Equals(ipEndPoint);
            }
            return false;
        }
        public override readonly int GetHashCode()
        {
            return ipEndPoint.GetHashCode();
        }
    }
}
