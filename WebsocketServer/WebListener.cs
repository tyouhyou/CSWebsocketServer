using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Threading;

namespace WebListener
{
  // TODO: background worker to check and clear the dead socket at a spectified interval?
  
  public class AsyncTCPListener
  {
    private Socket                                    mListenSocket      = null;
    private List<IWebListeningHandler>                mWebHandlers       = null;
    private Dictionary<string, SocketAsyncEventArgs>  mClientSockArg     = null;
    private Dictionary<string,IWebListeningHandler>   mClientHandler     = null;
    private WebListenerConfig                         mConfig            = null;

    private EventArgsPoolManager                      mEventArgsPool     = null;
    private BufferPoolManager                         mBufManager        = null;
    private EventHandler<SocketAsyncEventArgs>        mIOCompletedEvent  = null;

    public AsyncTCPListener()
    {
      Init();
    }

    public AsyncTCPListener(WebListenerConfig config)
    {
      this.mConfig = config;
      Init();
    }

    private void Init()
    {
      try
      {
        mWebHandlers = new List<IWebListeningHandler>();
        mClientHandler = new Dictionary<string, IWebListeningHandler>();
        mClientSockArg = new Dictionary<string, SocketAsyncEventArgs>();
        if (null == mConfig)
        {
          mConfig = new WebListenerConfig();
        }

        mBufManager = new BufferPoolManager(mConfig.MaxBufferSize, mConfig.MaxConnection);
        mEventArgsPool = new EventArgsPoolManager(mConfig.MaxConnection);
        mIOCompletedEvent = new EventHandler<SocketAsyncEventArgs>(IOCompleted);
      }
      catch(Exception e)
      {
        throw new WebListenerException("Error occurred during initialization: " + e.ToString());
      }
    }

    public void AddHandler(IWebListeningHandler handler)
    {
      mWebHandlers.Add(handler);
    }

    /// <summary>
    /// In case the spectified port is used by other app, 
    /// this method give a chance to change to another port,
    /// and call Start() again.
    /// 
    /// Once Start() has been called, this method has no effect on the listening.
    /// </summary>
    public void SetPort(int port)
    {
      if (IPEndPoint.MaxPort > port || IPEndPoint.MinPort < port)
      {
        throw new WebListenerException(
          string.Format("The port is out of range. It should be in ({0}-{1})",
                        IPEndPoint.MinPort,
                        IPEndPoint.MaxPort))
          {
            ERRORCODE = WebListenerException.ERROR_CODE_ILLEGALARG
          };
      }

      mConfig.Port = port;
    }

    public void Start()
    {
      try
      {
        IPEndPoint le = new IPEndPoint(IPAddress.Any, mConfig.Port);
        mListenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        mListenSocket.Bind(le);
        mListenSocket.Listen(mConfig.BackLog);

        DoAccept();
      }
      catch (Exception e)
      {
        throw new WebListenerException(e.Message);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="client"></param>
    /// <param name="data">
    /// The user should take the responsibility to prepare and dispose
    /// the data himself.
    /// </param>
    /// <param name="offset"></param>
    /// <param name="length"></param>
    /// <returns>
    /// The token id for user to check which data buffer can be released upon OnSent callback
    /// </returns>
    public void Send(string client, byte[] data, int offset, int length, out int tokenid)
    {
      tokenid = -1;

      SocketAsyncEventArgs args = null;
      try
      {
        args = mEventArgsPool.Get();
        args.SetBuffer(data, offset, length);
        args.Completed += mIOCompletedEvent;
      }
      catch(Exception)
      {
        // TODO: consider the situation that no eventargs in pool.
        // args = mClientSocArg[client];
      }

      if (args == null)
      {
        throw new WebListenerException("Illegal State.")
        {
          ERRORCODE = WebListenerException.ERROR_CODE_ILLEGALSTATE
        };
      }
      
      tokenid = ((AsyncUserToken)args.UserToken).TokenId;

      Socket s = mClientSockArg[client].AcceptSocket;
      args.AcceptSocket = s;
      try
      {
        if (!s.SendAsync(args))
        {
          OnSendComplete(args);
        }
      }
      catch (ObjectDisposedException)
      {
        Close(args);
        mClientHandler[client].OnClosed(client);
      }
    }

    public void Close(string client)
    {
      Close(mClientSockArg[client]);
    }

    public void Stop()
    {
      foreach (SocketAsyncEventArgs s in mClientSockArg.Values)
      {
        Close(s);
      }
      mEventArgsPool.Dispose();
      mBufManager.Dispose();
      mClientHandler = null;
    }

    private void Close(SocketAsyncEventArgs args)
    {
      Socket client = args.AcceptSocket;
      if (null != client)
      {
        mClientSockArg.Remove(GetClientIpInfo(client));
        mClientHandler.Remove(GetClientIpInfo(client));

        try
        {
          client.Shutdown(SocketShutdown.Both);
        }
        catch (Exception)
        {
          // Do nothing
        }
        client.Close();
        client = null;
      }
      args.AcceptSocket = null;
      mBufManager.ReleaseBuffer(args);
      mEventArgsPool.Put(args);
    }

    private void IOCompleted(object sender, SocketAsyncEventArgs args)
    {
      switch (args.LastOperation)
      {
        case SocketAsyncOperation.Accept:
          OnAcceptComplete(args);
          break;
        case SocketAsyncOperation.Receive:
          OnReceiveComplete(args);
          break;
        case SocketAsyncOperation.Send:
          OnSendComplete(args);
          break;
        default:
          throw new WebListenerException(string.Format("Last operation is {0} while Receive or Send is expected.", args.LastOperation));
      }
    }

    private void DoAccept()
    {
      DoAccept(null);
    }

    private void DoAccept(SocketAsyncEventArgs args)
    {
      if (args == null)
      {
        args = new SocketAsyncEventArgs();
        args.Completed += mIOCompletedEvent;
      }
      else
      {
        args.AcceptSocket = null;
      }

      if (!mListenSocket.AcceptAsync(args))
      {
        OnAcceptComplete(args);
      }
    }

    private void OnAcceptComplete(SocketAsyncEventArgs args)
    {
      Socket socket = args.AcceptSocket;

      if (!mClientSockArg.ContainsValue(args))
      {
        SocketAsyncEventArgs readEventArgs;
        try
        {
          readEventArgs = mEventArgsPool.Get();
        }
        catch (Exception)
        {
          // TODO: inform client about that server is busy
          return;
        }
      
        readEventArgs.AcceptSocket = socket;
        mBufManager.SetBuffer(readEventArgs);
        readEventArgs.Completed += mIOCompletedEvent;

        string client = GetClientIpInfo(socket);
        mClientSockArg.Add(client, readEventArgs);

        if (!DoReceive(readEventArgs))
        {
          OnReceiveComplete(readEventArgs);
        }
      }

      /* To do accept asap since there has only one eventarg for accept. */
      DoAccept(args);
    }

    private bool DoReceive(SocketAsyncEventArgs args)
    {
      return args.AcceptSocket.ReceiveAsync(args);
    }

    private void OnReceiveComplete(SocketAsyncEventArgs args)
    {
      string client = GetClientIpInfo(args.AcceptSocket);

      if (args.SocketError != SocketError.Success || args.BytesTransferred == 0)
      {
        try
        {
          mClientHandler[client].OnClosed(client);
          Close(args);
        }
        catch (Exception)
        {
          // Do nothing
        }
        return;
      }

      AsyncUserToken token = (AsyncUserToken)args.UserToken;
      try
      {
        if (token.Buf == null)
        {
          int buflen = args.BytesTransferred + args.AcceptSocket.Available;
          if (mBufManager.BufferSize < buflen)
          {
            token.Buf = new byte[buflen];
            token.Offset = 0;
            Buffer.BlockCopy(args.Buffer, args.Offset, token.Buf, token.ReceivedLength, args.BytesTransferred);
          }
          else
          {
            token.Buf = args.Buffer;
            token.Offset = args.Offset;
          }
        }
        
        token.ReceivedLength = token.ReceivedLength + args.BytesTransferred;
      }
      catch (SocketException)
      {
        mClientHandler[client].OnClosed(client);
        Close(args);
        token.Clear();
        return;
      }
      catch (Exception e)
      {
        token.Clear();
        throw new WebListenerException("Error occurred during receiving: " + e.ToString());
      }

      // keep receiving if there has remaining data in the queue
      if (0 != args.AcceptSocket.Available && !DoReceive(args))
      {
        OnReceiveComplete(args);
        return;
      }

      try
      {
        if (mClientHandler.ContainsKey(client))
        {
          mClientHandler[client].OnReceived(client, token.Buf, token.Offset, token.ReceivedLength);
          token.Clear();
        }
        else
        {
          foreach (IWebListeningHandler item in mWebHandlers)
          {
            mClientHandler.Add(client, item); // In case user will send data during the OnReceive event
            if (!item.OnReceived(client, token.Buf, token.Offset, token.ReceivedLength))
            {
              mClientHandler.Remove(client);
              break;
            }
          }
        }
      }
      catch (Exception e)
      {
        throw new WebListenerException("Error occurred during receiving: " + e.ToString());
      }
      finally
      {
        token.Clear();
      }
      
    }

    private void OnSendComplete(SocketAsyncEventArgs args)
    {
      string client = GetClientIpInfo(args.AcceptSocket);

      IWebListeningHandler handler = mClientHandler[client];

      if (args.SocketError == SocketError.Success)
      {
        mClientHandler[client].OnSent(client, args.BytesTransferred, ((AsyncUserToken)args.UserToken).TokenId);
      }
      else
      {
        handler.OnError(client, new WebListenerException("Error occurred."));
      }

      args.Completed -= mIOCompletedEvent;
      args.SetBuffer(null, 0, 0);
      mEventArgsPool.Put(args);
    }

    private string GetClientIpInfo(Socket socket)
    {
      IPEndPoint client = (IPEndPoint)socket.RemoteEndPoint;
      return client.Address.ToString() + ":" + client.Port.ToString();
    }

    /* ------------------ Private Classes --------------------*/

    private class AsyncUserToken
    {
      private byte[] _Buf = null;
      private int _ReceivedLength = 0;
      private int _Offset = 0;

      public int TokenId
      {
        get;
        set;
      }

      public byte[] Buf
      {
        get
        {
          return _Buf;
        }
        set
        {
          _Buf = value;
        }
      }

      public int ReceivedLength
      {
        get
        {
          return _ReceivedLength;
        }
        set
        {
          _ReceivedLength = value;
        }
      }

      public int Offset
      {
        get
        {
          return _Offset;
        }
        set
        {
          _Offset = value;
        }
      }

      public void Clear()
      {
        _Buf = null;
        _ReceivedLength = 0;
        _Offset = 0;
      }
    }

    private class EventArgsPoolManager
    {
      private const int mWaitTimeOut = 1000;

      private Stack<SocketAsyncEventArgs> mPool = null;
      private object mSyncroot = null;
      private Semaphore mSema = null;
      private int mCapacity = 0;

      public EventArgsPoolManager(int capacity)
      {
        mPool = new Stack<SocketAsyncEventArgs>(capacity);
        mSyncroot = new object();
        mCapacity = capacity;
        mSema = new Semaphore(capacity, capacity);

        for (int i = (capacity - 1); i >= 0; i--)
        {
          AsyncUserToken token = new AsyncUserToken() { TokenId = i };
          SocketAsyncEventArgs arg = new SocketAsyncEventArgs();
          arg.UserToken = token;
          mPool.Push(arg);
        }
      }

      public void Dispose()
      {
        foreach (SocketAsyncEventArgs arg in mPool)
        {
          arg.SetBuffer(null, 0, 0);
          arg.UserToken = null;
        }

        mPool = null;
        mSyncroot = null;
        mSema = null;
      }

      public void Put(SocketAsyncEventArgs arg)
      {
        lock (mSyncroot)
        {
          if (null == arg || mPool.Count >= mCapacity)
          {
            throw new WebListenerException("Null argument or there has no space to contain the object.");
          }
          mPool.Push(arg);
        }
        mSema.Release();
      }

      public SocketAsyncEventArgs Get()
      {
        mSema.WaitOne(mWaitTimeOut);
        lock (mSyncroot)
        {
          if (mPool.Count <= 0)
          {
            throw new WebListenerException("There has now object in the pool.")
            {
              ERRORCODE = WebListenerException.ERROR_CODE_BUSY
            };
          }
          SocketAsyncEventArgs arg = mPool.Pop();
          ((AsyncUserToken)arg.UserToken).Clear();
          return arg;
        }
      }
    }

    private class BufferPoolManager
    {
      private byte[] mBuf = null;
      private object mBufLocker = null;
      private Stack<int> mFreeBufStack = null;

      private int mMaxBufSize = 0;
      private int mUnitBufSize = 0;
      private int mCurrentIndex = 0;

      public int BufferSize
      {
        get
        {
          return mUnitBufSize;
        }
      }

      public BufferPoolManager(int maxbuffersize, int bufnumber)
      {
        mUnitBufSize = maxbuffersize / bufnumber;
        //if (mBufUnitSize < 256)    // magic number here
        //{
        //  throw new WebListenerException("Buffer size is too small.")
        //  {
        //    ERRORCODE = WebListenerException.ERROR_CODE_ILLEGALARG
        //  };
        //}

        this.mMaxBufSize = maxbuffersize;

        mBuf = new byte[mMaxBufSize];

        mFreeBufStack = new Stack<int>(bufnumber);

        mBufLocker = new object();
      }

      public void Dispose()
      {
        mBuf = null;
        mFreeBufStack = null;
        mBufLocker = null;
      }

      public void SetBuffer(SocketAsyncEventArgs arg)
      {
        if (null == arg)
        {
          throw new WebListenerException("Illegal Argument: null argument.");
        }

        int offset = 0;

        if (mFreeBufStack.Count > 0)
        {
          offset = mFreeBufStack.Pop();
        }
        else
        {
          if (mMaxBufSize - mCurrentIndex < mUnitBufSize)
          {
            throw new WebListenerException("No spare space in the buffer.");
          }

          offset = mCurrentIndex;
          mCurrentIndex += mUnitBufSize;
        }

        arg.SetBuffer(mBuf, offset, mUnitBufSize);
      }

      public void ReleaseBuffer(SocketAsyncEventArgs arg)
      {
        mFreeBufStack.Push(arg.Offset);
        arg.SetBuffer(null, 0, 0);
      }
    }
  
  }
}
