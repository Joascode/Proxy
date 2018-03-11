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

namespace ProxyApp
{
    class RequestHandler
    {
        MainWindow window;
        TcpListener listener;
        TcpClient client;
        NetworkStream ns;
        private StringBuilder sb = new StringBuilder();
        private static readonly string[] _imageExtensions = { "jpg", "bmp", "gif", "png" };
        
        private Action<string, Types> callback2;
        public enum Types
        {
            request,
            response
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
                string message = ReadMessage();

                if (removeBrowser) message = RemoveBrowserHeader(message);

                callback(message, Types.request);
                
                string url = GetUrlFromRequest(message);
                if(url != null)
                {
                    //Make this more pretty. RequestForImage only requires url so this can be done inside ReachSite.
                    if (RequestForImage(message)) url = "file:///E:/ProgrammingProjects/WIN/ProxyApp/ProxyApp/images/placeholder.png";
                    string[] response = ReachSite(url);

                    if(response !=  null)
                    {
                        callback(response[0], Types.response);

                        WriteMessage(response[1]);
                    }
                }
            }
            
            client.Close();
        }

        private string ReadMessage()
        {
            string message = "";
            do
            {
                int messageLength = ns.Read(buffer, 0, buffer.Length);
                sb.AppendFormat("{0}", Encoding.UTF8.GetString(buffer, 0, messageLength));
                message = sb.ToString();
                
            } while (client.GetStream().DataAvailable);

            sb.Clear();
            buffer = new byte[bufferSize];

            return message;    
        }

        private void WriteMessage(string message)
        {
            if (message != null)
            {
                int messageSize = Encoding.UTF8.GetByteCount(message);
                buffer = Encoding.UTF8.GetBytes(message);
                ns.Write(buffer, 0, messageSize);
                ns.Flush();
            }
            else
            {
                //Change to callback function.
                window.AddToLog("Requested site is not reachable.");
            }

            buffer = new byte[bufferSize];
        }

        private string[] ReachSite(string url)
        {
            Uri uri = null;
            Uri sysUri = null;
            try
            {
                if (url == "file:///E:/ProgrammingProjects/WIN/ProxyApp/ProxyApp/images/placeholder.png")
                {
                    sysUri = new Uri("E:\\ProgrammingProjects\\WIN\\ProxyApp\\ProxyApp\\images\\placeholder.png");
                } else
                {
                    uri = new Uri(url);
                }
                try
                {
                    HttpWebRequest webRequest;
                    if (sysUri != null)
                    {
                        var sysConverted = sysUri.LocalPath;
                        //This can't be done. It's a FileRequest and not a HttpWebRequest.
                        //TODO: This can't be done. It's a FileRequest and not a HttpWebRequest.
                        webRequest = (HttpWebRequest)WebRequest.Create(sysConverted);
                    } else
                    {
                        webRequest = (HttpWebRequest)WebRequest.Create(uri);
                    }
                    
                    webRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    try
                    {
                        using (HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse())
                        using (Stream stream = response.GetResponseStream())
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            foreach (string header in response.Headers)
                                sb.AppendFormat("{0}:{1} \n", header, response.Headers[header]);
                            sb.Append("\n");
                            sb.Append("\n");

                            string headers = sb.ToString();
                            string message = reader.ReadToEnd();

                            sb.Clear();

                            return new string[] { headers, message };
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
            string[] headers = request.Split(' ');
            foreach(string imageType in _imageExtensions)
            {
                if (headers[1].EndsWith(imageType))
                {
                    Console.WriteLine("Request for image made.");
                    return true;
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
                    Console.WriteLine(header);
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
            string newUrl = "127.0.0.1:8080/images/placeholder.png";
            headers[1] = newUrl;

            return newUrl;
        }
    }
}
