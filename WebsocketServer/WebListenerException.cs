using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebListener
{
  public class WebListenerException : ApplicationException
  {
    public static int ERROR_CODE_UNKNOWN = 0;
    public static int ERROR_CODE_BUSY = 1;
    public static int ERROR_CODE_ILLEGALARG = 2;
    public static int ERROR_CODE_ILLEGALSTATE = 3;
    public static int ERROR_CODE_TIMEOUT = 4;

    public WebListenerException(string message)
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
