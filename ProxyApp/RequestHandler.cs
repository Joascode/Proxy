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
        NetworkStream stream;
        private StringBuilder sb = new StringBuilder();
        private static readonly string[] _imageExtensions = { "jpg", "bmp", "gif", "png", "jpeg" };

        private Dictionary<string, Message> CachedMessages = new Dictionary<string, Message>();
        
        private Action<string, Types> callback2;
        public enum Types
        {
            request,
            response,
            log
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

        private bool basicAuth = false;

        public RequestHandler(MainWindow window, int port, int bufferSize)
        {
            this.window = window;
            Port = port;
            BufferSize = bufferSize;
            buffer = new byte[bufferSize];
        }

        //TODO: FIX CALLBACK MESSAGE TO RETURN A MESSAGE OBJECT.
        public async void Listen(Action<string, Types> callback)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            callback($"Listening on port: {port}", Types.log);
            while (true)
            {
                try
                {
                    client = await listener.AcceptTcpClientAsync();
                    if(client != null)
                    {
                        await Task.Run(() => HandleMessages(callback));
                    }
                    
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


        //REVAMP TO SUPPORTS 4-way MESSAGES. Client > Proxy > Server > Proxy > Client.
        private void HandleMessages(Action<string, Types> callback)
        {
            using (ns = client.GetStream())
            {
                Message request = ReceiveRequest(callback);

                TcpClient server = new TcpClient();

                try
                {
                    server.Connect(request.GetHostFromRequest(), 80);

                    //if (removeBrowser) request.RemoveBrowserHeader("User-Agent");

                    ForwardRequest(server, callback, request.GetMessageAsByteArray());

                    Message response = ReceiveResponse(callback);

                    /*if (replaceImages) {
                        if(response.RequestForImage())
                        {
                            response = RequestPlaceholder(PLACEHOLDER_URL);
                        }
                    }*/

                    /*if(basicAuth)
                    {
                        response.AddBasicAuth();
                        if(CheckAuthentication(response.GetHeadersAsString()))
                        {
                            ForwardResponse(callback, response.byteMessage);
                        }
                    }*/

                    ForwardResponse(callback, response.GetMessageAsByteArray());
                }
                catch(Exception e) when (
                    e is ArgumentNullException ||
                    e is ArgumentOutOfRangeException ||
                    e is SocketException ||
                    e is ObjectDisposedException
                )
                {
                    callback($"An Error has occurred: {e.Message}", Types.log);
                } finally
                {
                    server.Close();
                }
            }
            
            client.Close();
        }

        private Message ReceiveRequest(Action<string, Types> callback)
        {
            //http://www.yoda.arachsys.com/csharp/readbinary.html
            using (var mstrm = new MemoryStream())
            {
                var tempBuffer = new byte[1024];
                int bytesRead;
                if (ns.CanRead)
                {
                    if (ns.DataAvailable)
                    {
                        do
                        {
                            bytesRead = ns.Read(tempBuffer, 0, tempBuffer.Length);
                            mstrm.Write(tempBuffer, 0, bytesRead);

                        } while (ns.DataAvailable);
                    }
                }
                buffer = mstrm.GetBuffer();
                mstrm.Flush();
            }

            Console.WriteLine("Data size: " + buffer.Length);
            Console.WriteLine("Response: " + Encoding.ASCII.GetString(buffer, 0, buffer.Length));

            Message messageObj = new Message(buffer);

            buffer = new byte[bufferSize];

            return messageObj;
        }

        private void ForwardRequest(TcpClient server, Action<string, Types> callback, byte[] request)
        {
            //Dit is echt heel lelijk, ik start deze stream liever ergens anders. Komt later.
            stream = server.GetStream();
            if (request != null)
            {
                callback("Forwarding request.", Types.log);
                stream.Write(request, 0, request.Length);
                stream.Flush();
            }
            else
            {
                //Change to callback function.
                callback("Requested site is not reachable.", Types.log);
            }
        }

        private Message ReceiveResponse(Action<string, Types> callback)
        {

            //http://www.yoda.arachsys.com/csharp/readbinary.html
            using (var mstrm = new MemoryStream())
            {
                var tempBuffer = new byte[1024];
                int bytesRead;
                if (stream.CanRead)
                {
                    Thread.Sleep(1000);
                    if (stream.DataAvailable)
                    {
                        do
                        {
                            bytesRead = stream.Read(tempBuffer, 0, tempBuffer.Length);
                            mstrm.Write(tempBuffer, 0, bytesRead);
                        } while (stream.DataAvailable);
                    }
                }

                buffer = mstrm.GetBuffer();
                mstrm.Flush();
            }

            Console.WriteLine("Data size: " + buffer.Length);
            Console.WriteLine("Response: " + Encoding.ASCII.GetString(buffer, 0, buffer.Length));

            Message messageObj = new Message(buffer);

            buffer = new byte[bufferSize];

            return messageObj;
        }

        private void ForwardResponse(Action<string, Types> callback, byte[] response)
        {
            
            if (response != null)
            {
                callback("Forwarding Response.", Types.log);
                ns.Write(response, 0, response.Length);
                ns.Flush();
            }
            else
            {
                //Change to callback function.
                callback("Requested site is not reachable.", Types.log);
            }
        }

        //Place into request object.
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

        private bool CheckAuthentication(string response)
        {
            string[] headers = response.Split('\n');
            foreach (string header in headers)
            {
                if (header.StartsWith("Authentication: "))
                {
                    string username = "admin";
                    string password = "admin";
                    string encoded = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
                    string[] auth = header.Split(new[] { ": " }, StringSplitOptions.None);
                    if (auth[1].Equals("Basic " + encoded)) return true;
                }
            }
            return false;
        }
    }
}
