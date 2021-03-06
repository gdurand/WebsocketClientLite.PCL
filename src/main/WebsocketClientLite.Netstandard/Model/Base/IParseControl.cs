﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebsocketClientLite.PCL.Model.Base
{
    internal interface IParseControl
    {
        bool IsEndOfMessage { get; }

        bool IsRequestTimedOut { get; }

        bool IsUnableToParseHttp { get; }
    }
}
