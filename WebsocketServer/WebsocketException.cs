using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebSocket
{
  class WebsocketException : ApplicationException
  {
    public static int ERROR_CODE_UNKNOWN = 0;
    public static int ERROR_CODE_ILLEGALARG = 1;
    public static int ERROR_CODE_ILLEGALSTATE = 2;

    public WebsocketException(string message)
      : base(message)
    {
      ERRORCODE = ERROR_CODE_UNKNOWN;
    }

    public int ERRORCODE
    {
      get;
      set;
    }
  }
}
