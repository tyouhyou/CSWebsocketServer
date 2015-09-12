using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WebListener;
using WebListenerTest;
using System.Threading;

namespace WebSocketServer
{
  class AppRunner : ApplicationContext
  {
    public AppRunner()
    {
      #region console form handle
      if (AppConfig.VisualMode)
      {
        ConsoleView controlView = new ConsoleView();
        controlView.Visible = true;
        controlView.Closed += new EventHandler(this.OnFormClosed);
      }
      #endregion

      // TODO: register windows message handler here

      // To avoid influting the UI thread.
      Thread worker = new Thread(StartToListen);
      worker.IsBackground = true;
      worker.Start();
    }

    private void StartToListen()
    {
      AsyncTCPListener mListener = new AsyncTCPListener(new WebListenerConfig()
      {
        Port = 8008,
        MaxBufferSize = 10 * 1024 * 1024,
        MaxConnection = 1000,
        BackLog = 100
      });

      /* ---------- TCP LISTENER TEST S ----------- */
      //WebListenerTester tester = new WebListenerTester(mListener);
      /* ---------- TCP LISTENER TEST E ----------- */

      /* ---------- WEBSOCKET SERVER S ------------ */
      WebsocketServer mWebsocketServer = new WebsocketServer(mListener);
      //IWebsocketSubProtocol mSub1 = new ();
      //mWebsocketServer.addSubProtocol(mSub1);
      /* ---------- WEBSOCKET SERVER E ------------ */

      /* ---------- HTTP PROXY S ------------------ */
      //proxy server goes here
      /* ---------- HTTP PROXY E ------------------ */

      mListener.Start();
    }

    private void OnFormClosed(object sender, EventArgs e)
    {
      ExitThread();
    }
  }
}
