using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebListener;

namespace WebListenerTest
{
  class WebListenerTester : IWebListeningHandler
  {
    private AsyncTCPListener mListener;
    private List<int> mTokenIds;

    //public void start()
    //{
    //  mTokenIds = new List<int>();
    //  mListener = new AsyncTCPListener(new WebListenerConfig() 
    //  {
    //    Port=8008, 
    //    MaxBufferSize=10*1024*1024, 
    //    MaxConnection=10000, 
    //    BackLog=100
    //  });
    //  mListener.AddHandler(this);
    //  mListener.Start();
    //}

    public WebListenerTester(AsyncTCPListener listener)
    {
      mTokenIds = new List<int>();
      mListener = listener;
      mListener.AddHandler(this);
    }

    public void send(string client, byte[] data)
    {
      int tokenid;
      mListener.Send(client, data, 0, data.Length, out tokenid);
      if (!mTokenIds.Contains(tokenid))
      {
        mTokenIds.Add(tokenid);
      }
    }

    private void broadCast(byte[] data)
    {
      // TODO: set a broad cast sample here.
    }

    public bool OnReceived(string client, byte[] buffer, int offset, int buflen)
    {
      string request = Encoding.UTF8.GetString(buffer, offset, buflen);
      send(client, (new UTF8Encoding()).GetBytes("HTTP/1.1 200 OK\r\n\r\nGoodbye, crutial world\r\nI got requests as follows:\r\n" + request));

      return true;
    }

    public void OnSent(string client, int byteSent, int tokenid)
    {
      if (mTokenIds.Contains(tokenid))
      {
        mTokenIds.Remove(tokenid);
        // do something.
      }
      else
      {
        // illegal state.
      }
    }

    public void OnError(string client, WebListenerException e){}

    public void OnClosed(string client){}

    public void OnStopped(){}
  }
}
