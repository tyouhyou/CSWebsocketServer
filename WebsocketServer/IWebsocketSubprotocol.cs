using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WebSocket
{
  interface IWebsocketSubprotocol
  {
    String Name
    { 
      get;
    }
    
    /// <summary>
    /// Defaultly, use this method to receive application data.
    /// If websocket extension is desired, add new method in this
    /// interface and in WebsocketImpl#Dataparser
    /// </summary>
    /// <param name="type"></param>
    /// <param name="data"></param>
    /// <param name="offset"></param>
    /// <param name="datasize"></param>
    void OnMessage(WebsocketImplS.DataFrame dataFrame);
    void OnClosed();
    void OnError();
    void Close();
    void Send(byte[] data);
  }
}
