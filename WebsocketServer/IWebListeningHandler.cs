
namespace WebListener
{
  /// <summary>
  /// Ususally, with C#, callbacks should be designed as EVENT.
  /// But if we do so, one more pool should be maintained to keep
  /// the EventArgs in the WebListener.
  /// <br>
  /// To reduce the memory / performance cost, callback is a better choice.
  /// </summary>
  public interface IWebListeningHandler
  {
    /// <summary>
    /// Upon receiving data, the listener informs users of the receiving
    /// with this method.<br>
    /// Note:<br>
    /// When this method returns, the buffer will be clearred. Thus, 
    /// processing the buffer data synchronoursly is desirable. Or,
    /// if the processing is time consumming, copy the data before
    /// the method returns.
    /// </summary>
    /// <param name="client">Client ip and port.</param>
    /// <param name="buffer">Received data.</param>
    /// <param name="offset">The offset of data in the buffer.</param>
    /// <param name="bytelength">
    /// The length of the received data.
    /// Never count on the buffer.length to determine the buffer length,
    /// alternatively, use this parameter.
    /// </param>
    /// <returns></returns>
    bool OnReceived(string client, byte[] buffer, int offset, int bytelength);

    /// <summary>
    /// When the listener has sent a block of data successfully,
    /// it inform the user with this callback.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="byteSent"></param>
    /// <param name="tokenId">
    /// When use the Send() API of [Weblistener], it returns a tokenId. 
    /// Check the tokenId received here with the one returned from Send(),
    /// It could be determined which sending process has been finished.
    /// </param>
    void OnSent(string client, int byteSent, int tokenId);

    void OnClosed(string client);

    void OnStopped();

    void OnError(string client, WebListenerException e);
  }
}
