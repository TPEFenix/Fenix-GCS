﻿using System;
using System.Collections.Generic;
using System.Text;

namespace FenixGCSApi
{
    public enum EMsgType : int
    {
        None,
        Unknown,
        Login,
        Free,
        LoginRtn,
        LoginHint,
        TalkMsgTo,
        MsgRecv,
        FunctionCall
    }
}
