using System;
using MessagePack;

namespace FenixGCSApi
{
    [Union((int)EMsgType.Login, typeof(GCSCommand_Login_Request))]
    [Union((int)EMsgType.LoginRtn, typeof(GCSCommand_Login_Response))]
    [MessagePackObject(true)]
    public abstract partial class GCSCommandPack
    {
        public EMsgType EMsgType { get; set; }
        public string ID { get; set; }
        public bool IsRequest { get; set; }
        public string ResponseTo { get; set; }

        public GCSCommandPack(EMsgType eMsgType, bool isRequest = false, string responseTo = null)
        {
            ID = GUIDGetter.Get();
            EMsgType = eMsgType;
            IsRequest = isRequest;
            ResponseTo = responseTo;
        }
    }
}
