using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Net;

namespace WebSocket
{
  /// <summary>
  /// This class implements the websocket specification for server side.
  /// </summary>
  public class WebsocketImplS
  {
    public static string SEC_WEBSOCKET_KEY = "Sec-Websocket-Key";
    public static string SEC_WEBSOCKET_PROTOCOL = "Sec-Websocket-Protocol";
    public static string SEC_WEBSOCKET_EXTENSIONS = "Sec-Websocket-Extensions";
    public static string SEC_WEBSOCKET_PROTOCOL_SEPERATOR = ",";

    public enum OpCode : byte
    {
      CONT = 0,
      TEXT = 1,
      BIN = 2,
      CLOSE = 8,
      PING = 9,
      PONG = 10,
    }

    public enum CloseStatusCode
    {
      NORMAL_CLOSE = 1000,
      GOING_AWAY = 1001,
      PROTOCOL_ERROR = 1002,
      UNKNOWN_DATA = 1003,
      UNCONSISTENT_DATA = 1007,
      UNEXPECTED_DATA = 1008,
      TOO_BIG_DATA = 1009,
      NO_EXPECTED_EXTENSION = 1010,
      UNEXPECTED_CONDITION = 1011,
    }

    public enum ClientStatus
    {
      OPENING = 1,
      OPENED = 2,
      // ...
      CLOSING = 9,
      CLOSED = 10,
    }

    public static  Dictionary<int, string> HttpStatusTable = new Dictionary<int, string>
    {
      {101, "Switching Protocols"},
      {301, "Moved Permanently"},
      {400, "Bad Request"},
      {401, "Unauthorized"},
      {403, "Forbidden"},
      {405, "Method Not Allowed"},
      {406, "Not Acceptable"},
      {500, "Internal Server Error"},
    };

    public struct DataFrame
    {
      public bool IsFin;
      public byte Rsv1;
      public byte Rsv2;
      public byte Rsv3;
      public OpCode OpCode;
      public bool IsMasked;
      public Int64 DataLength;
      public int MaskingKey;
      public short DataOffset;
      public byte[] Data;
    }

    /* ----------- Nested Classes -------------*/

    public class HandShaker
    {
      private static string[] HttpPartsSeperators  = new string[] { "\r\n\r\n" };
      private static string[] HeaderSeperators     = new string[] { "\r\n" };
      private static string[] ReqLineSeperators    = new string[] { " " };
      private static string   FieldSeperators      = ":";

      private static string   HttpKeyWords         = "HTTP";
      private static string   Getmethod            = "GET";
      private static string   HttpVersion          = "HTTP/1.1";
      
      public bool IsHandShaking(byte[] data, int offset, int datasize, out Dictionary<string, string> requestFields)
      {
        bool ret = false;
        requestFields = new Dictionary<string, string>();

        try
        {
          string method = Encoding.UTF8.GetString(data, offset, Getmethod.Length);
          // check method
          if (!method.ToUpper().Equals(Getmethod))
          {
            return ret;
          }

          string request = Encoding.UTF8.GetString(data, offset, datasize);
          string header = (request.Split(HttpPartsSeperators, StringSplitOptions.RemoveEmptyEntries))[0];
          string[] fields = header.Split(HeaderSeperators, StringSplitOptions.RemoveEmptyEntries);

          string requesLine = fields[0];
          if (requesLine != null)
          {
            string[] rline = requesLine.Split(ReqLineSeperators, StringSplitOptions.RemoveEmptyEntries);

            string resourcename = rline[1];
            // check resource name
            if (null == resourcename)
            {
              // According to rfc6455 chapter4.1, the resource-name should be checked.
              // At this time being, just check null. TODO it later.
              return ret;
            }

            string httpversion = rline[2];
            string htk = httpversion.Substring(0, HttpKeyWords.Length);
            if (!htk.ToUpper().Equals(HttpKeyWords))
            {
              return ret;
            }

            float version = float.Parse(httpversion.Substring(HttpKeyWords.Length + 1));
            // check http version
            if (version < 1.1)
            {
              return ret;
            }

            for (int i = 1; i < fields.Length; i++)
            {
              string field = fields[i];
              int idx = field.IndexOf(FieldSeperators);
              string key = field.Substring(0, idx).Trim().ToLower(); // key cannot be null
              string value = field.Substring(idx + 1);
              if (value != null)
              {
                value = value.Trim();
              }
              else
              {
                value = string.Empty;
              }

              requestFields.Add(key, value);
            }
            requestFields.Add("resource-name", resourcename);

            // check host field
            if (!requestFields.ContainsKey("host"))
              return ret;

            // check upgrade field
            if (!requestFields.ContainsKey("upgrade") ||
                !"websocket".Equals(requestFields["upgrade"]))
              return ret;

            // check connection filed
            if (!requestFields.ContainsKey("connection") ||
                !"Upgrade".Equals(requestFields["connection"]))
              return ret;

            // check key. TODO the base64's check might be done in the future.
            if (!requestFields.ContainsKey("sec-websocket-key"))
            {
              byte[] b = System.Convert.FromBase64String(requestFields["sec-websocket-key"]);
              string s = Encoding.UTF8.GetString(b);
              if (16 != s.Length) return ret;
            }

            // check websocket version
            if (!requestFields.ContainsKey("sec-websocket-version") ||
                13 != int.Parse(requestFields["sec-websocket-version"]))
              return ret;

            ret = true;
          }
        }
        catch (Exception)
        {
          requestFields = null;
          // TODO: log
        }

        return ret;
      }

      public string ShakeHands(string sec_key, string protocol, string extensions)
      {
        string res = InitResponse(101);

        AddResponseHeader("Upgrade", "websocket", ref res);
        
        AddResponseHeader("Connection", "Upgrade", ref res);
        
        if (!string.IsNullOrEmpty(sec_key))
        {
          string magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

          var sha1 = new SHA1CryptoServiceProvider();
          var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(sec_key + magic));
          string accept = Convert.ToBase64String(hash);

          AddResponseHeader("Sec-WebSocket-Accept", accept, ref res);
        }
        
        if (!string.IsNullOrEmpty(extensions))
          AddResponseHeader("Sec-WebSocket-Extensions", extensions, ref res);
        
        if (!string.IsNullOrEmpty(protocol))
          AddResponseHeader("Sec-WebSocket-Protocol", protocol, ref res);

        return res;
      }

      public string InitResponse(int statuscode)
      {
        string ret = null;

        ret = HttpVersion 
            + ReqLineSeperators[0] 
            + statuscode.ToString() 
            + ReqLineSeperators[0] 
            + HttpStatusTable[statuscode]
            + HttpPartsSeperators[0];

        return ret;
      }

      public void AddResponseHeader(string key, string value, ref string httpheaders)
      {
        if (null == httpheaders           ||
            string.IsNullOrEmpty(key)     ||
            string.IsNullOrEmpty(value)) 
          return;
        
        int idx = httpheaders.IndexOf(HttpPartsSeperators[0]);
        if (idx >= 0)
          httpheaders = httpheaders.Remove(idx);

        if (!httpheaders.EndsWith(HeaderSeperators[0]))
          httpheaders += HeaderSeperators[0]; 

        httpheaders += key + ": " + value + HttpPartsSeperators[0];
      }
    }

    public class DataParser
    {
      public DataFrame Parse(byte[] dataFrame)
      {
        return Parse(dataFrame, true);
      }

      public DataFrame Parse(byte[] dataFrame, bool toCopyData)
      {
        if (null == dataFrame)
          throw new WebsocketException("Data parse failed.")
          {
            ERRORCODE = WebsocketException.ERROR_CODE_ILLEGALARG
          };

        DataFrame df = new DataFrame();
        short curIdx;

        /* -------------- Parse 1st byte ---------------*/
        curIdx = 0;

        df.IsFin = Convert.ToBoolean((byte)(0x80 & dataFrame[curIdx]));

        df.Rsv1 = (byte)(0x40 & dataFrame[curIdx]);

        df.Rsv2 = (byte)(0x20 & dataFrame[curIdx]);

        df.Rsv3 = (byte)(0x10 & dataFrame[curIdx]);

        df.OpCode = (OpCode)(0x0F & dataFrame[curIdx]);

        /* -------------- Parse 2nd byte ---------------*/
        curIdx++;

        df.IsMasked = Convert.ToBoolean((byte)(0x80 & dataFrame[curIdx]));
        if (!df.IsMasked)
          throw new WebsocketException("The data are not masked")
          {
            ERRORCODE = WebsocketException.ERROR_CODE_ILLEGALSTATE
          };

        int nextn = 0;
        Int64 len = (Int64)(0x8F & dataFrame[curIdx]);
        if (125 >= len)
        {
          df.DataLength = len;
        }
        else if (126 == len)
        {
          nextn = 2;
        }
        else
        {
          nextn = 8;
        }

        /* -------------- Parse payload lengh ---------------*/
        if (nextn > 0)
        {
          for (int i = 0; i < nextn; i++)
          {
            curIdx++;
            len = len << 1;
            len = len | (0xFF & (uint)dataFrame[curIdx]);
          }

          df.DataLength = IPAddress.NetworkToHostOrder(len);  // to local endian
        }

        /* -------------- Parse masking key ---------------*/
        int maskkey = 0;
        for (int i = 0; i < 4; i++)
        {
          curIdx++;
          maskkey = maskkey << 1;
          maskkey = maskkey | (0xFF & dataFrame[curIdx]);
        }
        df.MaskingKey = maskkey;

        /* -------------- Parse payload data ---------------*/
        if (toCopyData)
        {
          int offset = 0;
          df.Data = new byte[df.DataLength];
          int round = (int)(df.DataLength / int.MaxValue);
          for (int i = 0; i <= round; i++)
          {
            int cnt;
            if (i == round)
            {
              cnt = (int)(df.DataLength % int.MaxValue);
            }
            else
            {
              cnt = int.MaxValue;
            }

            Buffer.BlockCopy(dataFrame, offset, df.Data, 0, cnt);
            offset += cnt;
          }
          df.DataOffset = 0;
        }
        else
        {
          df.Data = dataFrame;
          df.DataOffset = ++curIdx;
        }

        Unmask(df.Data, df.DataOffset, df.DataLength, df.MaskingKey);

        return df;
      }

      private void Unmask(byte[] data, int offset, Int64 size, int maskkey)
      {
        for (int i = offset; i < size; i++)
        {
          byte j = (byte)(i % 4);
          data[i] = (byte)(data[i] ^ j);
        }
      }
    }

    public byte[] DataFramToBytes(DataFrame frame)
    {
      byte[] data = null;
      // TODO
      return data;
    }
    
  }
}
