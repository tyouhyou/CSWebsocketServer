using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace WebSocketServer
{
  public class AppConfig
  {
    public static bool VisualMode
    {
      get
      {
        return bool.Parse(ConfigurationManager.AppSettings["VisualMode"]);
      }
    }
  }
}
