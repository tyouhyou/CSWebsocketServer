using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace WebSocketServer
{
  static class Program
  {
    [STAThread]
    static void Main()
    {
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      ApplicationContext context = new AppRunner();
      
      Application.Run(context);
    }
  }
}
