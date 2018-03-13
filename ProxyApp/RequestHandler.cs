using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace ProxyApp
{
    class RequestHandler
    {
        MainWindow window;
        TcpListener listener;
        TcpClient client;
        NetworkStream ns;
        private StringBuilder sb = new StringBuilder();
        private static readonly string[] _imageExtensions = { "jpg", "bmp", "gif", "png", "jpeg" };

        private Dictionary<string, Message> CachedMessages = new Dictionary<string, Message>();
        
        private Action<string, Types> callback2;
        public enum Types
        {
            request,
            response
        }

        private bool blockImages;
        public bool BlockImages
        {
            get
            {
                return blockImages;
            }
            set
            {
                blockImages = value;
            }
        }

        private bool removeBrowser;
        public bool RemoveBrowser
        {
            get
            {
                return removeBrowser;
            }
            set
            {
                removeBrowser = value;
            }
        }

        private int bufferSize;
        public int BufferSize
        {
            get
            {
                return bufferSize;
            }
            set
            {
                bufferSize = value;
                buffer = new byte[bufferSize];
            }
        }
        private byte[] buffer;

        private int port;
        public int Port
        {
            get
            {
                return port;
            }
            set
            {
                port = value;
            }
        }

        public RequestHandler(MainWindow window, int port, int bufferSize)
        {
            this.window = window;
            Port = port;
            BufferSize = bufferSize;
            buffer = new byte[bufferSize];
        }

        public async void Listen(Action<string, Types> callback)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            callback2 = callback;
            window.AddToLog($"Listening on port: {port}");
            while (true)
            {
                try
                {
                    client = await listener.AcceptTcpClientAsync();
                    await Task.Run(() => HandleMessages(callback));
                } catch(InvalidOperationException)
                {
                    //Geen nette manier van sluiten, maar nu geen tijd voor.
                    break;
                }
                    
            }
        }

        public void Close()
        {
            listener.Stop();
        }

        private void HandleMessages(Action<string, Types> callback)
        {
            using (ns = client.GetStream())
            {
                //Change to work with byte array. Use message object to handle string variation of returned message.
                string message = ReadMessage();

                if (removeBrowser) message = RemoveBrowserHeader(message);

                //Retrieve string array from message object to use the string variation of the byte array.
                callback(message, Types.request);

                
                //



                Message response;
                    //Url has to be returned from a method from the message object.
                string url = GetUrlFromRequest(message);
                if (url != null)
                {
                    response = ReachSite(url);
                    if (response != null)
                    {
                        if (blockImages && RequestForImage(response.GetHeadersAsString()))
                        {
                            var newurl = "http://clipground.com/images/placeholder-clipart-6.jpg";
                            response = ReachSite(newurl);
                        }

                        if (response != null)
                        {
                            //Retrieve string from message object to use the string variation of the byte array.
                            callback(response.GetMessageAsLog(), Types.response);

                            if(response.GetETag() != null)
                            {
                                
                                if(!CachedMessages.ContainsKey(url))
                                {
                                    CachedMessages.Add(url, response);
                                    Console.WriteLine("Cached site.");
                                }
                                   
                            }

                            //Change to work with byte array. Use message object to handle string variation of returned message.
                            WriteMessage(response.GetBody());
                        }
                    }
                }
            }
            
            client.Close();
        }

        //Change method to work with byte array.
        private string ReadMessage()
        {
            string message = "";
            do
            {
                int messageLength = ns.Read(buffer, 0, buffer.Length);
                sb.AppendFormat("{0}", Encoding.UTF8.GetString(buffer, 0, messageLength));
                message = sb.ToString();
                // message.ReadMessage(buffer, messageLength);
                
            } while (client.GetStream().DataAvailable);

            sb.Clear();
            buffer = new byte[bufferSize];

            return message;    
        }

        //Change method to work with byte array instead of string.
        private void WriteMessage(byte[] message)
        {
            if (message != null)
            {
                //int messageSize = Encoding.UTF8.GetByteCount(message);
                //byte[] outBuffer = Encoding.UTF8.GetBytes(message);
                ns.Write(message, 0, message.Length);
                ns.Flush();
            }
            else
            {
                //Change to callback function.
                window.AddToLog("Requested site is not reachable.");
            }

            //buffer = new byte[bufferSize];
        }

        //Change method to work with byte array and message object.
        private Message ReachSite(string url)
        {
            
            try
            {
                var uri = new Uri(url);
                try
                {
                    HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);
                    if (CachedMessages.TryGetValue(url, out Message message))
                    {
                        Console.WriteLine("Found ETag for url.");
                        webRequest.Headers.Add("ETag:" + message.GetETag());
                    }
                    webRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    try
                    {
                        using (HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse())
                        //using (Stream stream = response.GetResponseStream())
                        //using (BinaryReader reader = new BinaryReader(stream))
                        {
                            Console.WriteLine((int)response.StatusCode);
                            if (response.StatusCode.Equals("UNMODIFIED"))
                            {
                                Console.WriteLine("Return cached.");
                                return message;
                            }

                            byte[] data; // will eventually hold the result
                                         // create a MemoryStream to build the result
                            using (var mstrm = new MemoryStream())
                            {
                                using (var stream = response.GetResponseStream())
                                {
                                    var tempBuffer = new byte[4096];
                                    int bytesRead;
                                    while ((bytesRead = stream.Read(tempBuffer, 0, tempBuffer.Length)) != 0)
                                    {
                                        mstrm.Write(tempBuffer, 0, bytesRead);
                                    }
                                }
                                mstrm.Flush();
                                data = mstrm.GetBuffer();
                            }
                            /*foreach (string header in response.Headers)
                                sb.AppendFormat("{0}:{1} \n", header, response.Headers[header]);
                            sb.Append("\n");
                            sb.Append("\n");*/

                            //string headers = sb.ToString();
                            //Console.WriteLine(response.ContentLength);
                            //byte[] message = reader.ReadBytes((int)response.ContentLength);

                            Message messageObj = new Message(response.Headers, data);
                            

                            //sb.Clear();

                            return messageObj;
                        }
                    }
                    catch (Exception e) when (
                      e is WebException ||
                      e is InvalidOperationException ||
                      e is NotSupportedException ||
                      e is ProtocolViolationException
                    )
                    {
                        string errorMsg = "An error has occured. \n" +
                                "Error: " + e.Message + "\n" +
                                "Url: " + uri + "\n";

                        callback2(errorMsg, Types.response);
                        
                        Console.WriteLine("An error has occured: " + e.Message);
                        return null;
                    }
                } catch(Exception e) when (
                    e is NotSupportedException ||
                    e is ArgumentNullException ||
                    e is SecurityException
                )
                {
                    Console.WriteLine("An error has occured: " + e.Message);
                    return null;
                }
            } catch(Exception e) when (
                e is ArgumentNullException ||
                e is UriFormatException
            ) {
                Console.WriteLine("An error has occured: " + e.Message);
                return null;
            }
        }

        private string GetUrlFromRequest(string request)
        {
            string[] tokens = request.Split(' ');
            if (tokens.Length > 1)
            {
                return tokens[1];
            }
            else return null;
        }

        private bool RequestForImage(string request)
        {
            string[] headers = request.Split('\n');
            foreach (string header in headers)
            {
                if (header.StartsWith("Content-Type:"))
                {
                    foreach(string imageType in _imageExtensions)
                    {
                        if(header.EndsWith(imageType+" "))
                        {
                            Console.WriteLine("Bingo! " + header);
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private string RemoveBrowserHeader(string request)
        {
            string[] headers = request.Split('\n');
            foreach(string header in headers)
            {
                if(!header.StartsWith("User-Agent:"))
                {
                    sb.Append(header);
                }
            }
            string newMessage = sb.ToString();
            sb.Clear();

            return newMessage;
        }

        private string ChangeImageRequest(string request)
        {
            string[] headers = request.Split(' ');
            string newUrl = "http://clipground.com/images/placeholder-clipart-6.jpg";
            headers[1] = newUrl;

            return newUrl;
        }

        private string GrabETag(string request)
        {
            string[] headers = request.Split('\n');
            foreach (string header in headers)
            {
                Console.WriteLine(header);
                if (header.StartsWith("ETag:"))
                {
                    var newheader = header.Split(':');
                    Console.WriteLine(newheader);
                    sb.Append(newheader[1]);
                }
            }
            string ETag = sb.ToString();
            sb.Clear();

            return ETag;
        }

        private bool InCache(string ETag)
        {
            return CachedMessages.ContainsKey(ETag);
        }
    }
}
