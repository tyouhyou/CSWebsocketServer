using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebListener;
using WebSocket;

namespace WebSocketServer
{
  class WebsocketServer : IWebListeningHandler
  {
    private AsyncTCPListener                    mTcpListener  = null;
    private List<IWebsocketSubprotocol>         mSubProtocols = null;
    private Dictionary<string, WebClient>       mClients      = null;
    private WebsocketImplS.HandShaker           mHandShaker   = null;
    private WebsocketImplS.DataParser           mDataParse    = null;
    private List<int>                           mSendTokenIds = null;

    public WebsocketServer(AsyncTCPListener listener)
    {
      mSubProtocols = new List<IWebsocketSubprotocol>();
      mClients = new Dictionary<string, WebClient>();
      mHandShaker = new WebsocketImplS.HandShaker();
      mDataParse = new WebsocketImplS.DataParser();
      mSendTokenIds = new List<int>();

      mTcpListener = listener;
      mTcpListener.AddHandler(this);
    }

    public void AddSubProtocol(IWebsocketSubprotocol protocol)
    {
      mSubProtocols.Add(protocol);
    }

    // If it is my processing target, return true;
    public bool OnReceived(string client, byte[] buffer, int offset, int buflength)
    {
      bool ret = true;

      // It doesn't need new thread to process handshake.
      if (!mClients.ContainsKey(client))
      {
        Dictionary<string, string> requests = null;
        if (mHandShaker.IsHandShaking(buffer, offset, buflength, out requests))
        {
          // Authentication require/check goes here.

          string seckey = null;
          if (requests.ContainsKey(WebsocketImplS.SEC_WEBSOCKET_KEY.ToLower()))
            seckey = requests[seckey];
          else
            seckey = null;

          string subproto = null;
          if (requests.ContainsKey(WebsocketImplS.SEC_WEBSOCKET_PROTOCOL.ToLower()))
          {
            subproto = requests[subproto];
            // TODO: add sub protocol response
          }
          else
          {
            subproto = null;
          }

          string extensions = null;
          if (requests.ContainsKey(WebsocketImplS.SEC_WEBSOCKET_EXTENSIONS))
          {
            // TODO List: add extensions
          }

          string response = mHandShaker.ShakeHands(seckey, subproto, extensions);
          byte[] resp = Encoding.UTF8.GetBytes(response);
          Send(client, resp);

          // TODO: add client information
        }
        else
        {
          ret = false;
        }
      }
      else
      {
        // TODO: protocol onmessage.
        // use thread pool
      }

      return ret;
    }

    private void Send(string client, byte[] data)
    {
      int tokid;
      mTcpListener.Send(client, data, 0, data.Length, out tokid);
      mSendTokenIds.Add(tokid);
    }

    private void Ping()
    {
      // TODO
    }

    private void Pong()
    {
      // TODO
    }

    private void Close()
    {
      // TODO
    }

    public void OnSent(string client, int byteSent, int tokenId)
    {
      if (mSendTokenIds.Contains(tokenId))
      {
        mSendTokenIds.Remove(tokenId);
      }
      else
      {
        // illegal state. Log?
      }
    }

    public void OnError(string client, WebListenerException e)
    {
      // TODO
    }

    public void OnClosed(string client)
    {
      // TODO
    }

    public void OnStopped()
    {
      // TODO
    }
  }

  internal class WebClient
  {
    public WebsocketImplS.ClientStatus Status
    {
      get;
      set;
    }
    public IWebsocketSubprotocol Subprotocol
    {
      get;
      set;
    }
    public byte[] ReceivedData
    {
      get;
      set;
    }
  }
}
