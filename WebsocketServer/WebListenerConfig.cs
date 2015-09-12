using System.Net;

namespace WebListener
{
  public class WebListenerConfig
  {
    private int _backlog = 100;
    private int _port = 80;
    private int _maxconnect = 1000;
    private int _maxbuffersize = 1 * 1024 * 1024;

    public int BackLog
    {
      get
      {
        return _backlog;
      }
      set
      {
        if (0 > value)
        {
          throw new WebListenerException("BackLog should be greater than 0.");
        }
        _backlog = value;
      }
    }

    public int Port
    {
      get
      {
        return _port;
      }
      set
      {
        if (IPEndPoint.MinPort > value || IPEndPoint.MaxPort < value)
        {
          throw new WebListenerException(string.Format("port should be in range from {0} to {1}", IPEndPoint.MinPort, IPEndPoint.MaxPort));
        }
        _port = value;
      }
    }

    public int MaxConnection
    {
      get
      {
        return _maxconnect;
      }
      set
      {
        if (0 > value)
        {
          throw new WebListenerException("The max connection should be greater than 0.");
        }
        _maxconnect = value;
      }
    }

    public int MaxBufferSize
    {
      get
      {
        return _maxbuffersize;
      }
      set
      {
        if (0 > value)
        {
          throw new WebListenerException("The buffer size should be greater than 0.");
        }
        _maxbuffersize = (int)value;
      }
    }
  }
}
