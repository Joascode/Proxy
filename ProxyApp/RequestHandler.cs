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
        private TcpListener listener;
        private TcpClient client;
        private NetworkStream clientStream;
        private NetworkStream serverStream;
        private StringBuilder sb = new StringBuilder();

        private Dictionary<string, Message> CachedMessages = new Dictionary<string, Message>();

        private static readonly object _lock = new object();

        //private Action<string, Types> callback2;
        public enum Types
        {
            request,
            response,
            log
        }

        //MOET NOG GEKOPPELD WORDEN AAN UI.
        private long cacheDuration = 30000;
        public long CacheDuration
        {
            get
            {
                return cacheDuration;
            }
            set
            {
                cacheDuration = value;
            }
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

        public bool basicAuth = false;

        public RequestHandler(int port, int bufferSize)
        {
            Port = port;
            BufferSize = bufferSize;
            buffer = new byte[bufferSize];
        }

        //TODO: Fix callback to work without the Types enum. Isn't a clean way to create loosely coupled classes.
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
                    lock (CachedMessages) CheckForClearCache();

                } catch(InvalidOperationException)
                {
                    //Geen nette manier van sluiten, maar nu geen tijd voor.
                    break;
                }
                    
            }
        }

        //TODO: Too small, seems useless. See if this fits into a try/catch/finally block or using.
        public void Close()
        {
            listener.Stop();
        }


        //REVAMP TO SUPPORTS 4-way MESSAGES. Client > Proxy > Server > Proxy > Client.
        //TODO: Clean up this method, because it does too many things now. SOLID isn't implemented.
        private void HandleMessages(Action<string, Types> callback)
        {
            using (clientStream = client.GetStream())
            {
                Message request = ReceiveRequest(callback);
                Message response = null;

                //Check if request has been made earlier. If so, retrieve response tied to request.
                //TODO: Check for null return value from GetRequestUrl.
                if(request.GetRequestUrl() != null)
                {
                    //TODO: Check for changed content first, before getting it from the cache.
                    if (CachedMessages.TryGetValue(request.GetRequestUrl(), out response))
                    {
                        Console.WriteLine("Returning response from cache.");
                        Console.WriteLine(request.GetRequestUrl());
                        ForwardResponse(callback, response);
                    }
                    //Start a connection and grab response from server.
                    else
                    {
                        TcpClient server = new TcpClient();

                        try
                        {
                            server.Connect(request.GetHostFromRequest(), 80);

                            if (removeBrowser) request.RemoveBrowserHeader("User-Agent");

                            ForwardRequest(server, callback, request);

                            response = ReceiveResponse(callback);
                            
                            if (blockImages)
                            {
                                if (response.RequestForImage())
                                {
                                    //response = RequestPlaceholder(PLACEHOLDER_URL);
                                    Console.WriteLine("Asking for image.");
                                    response.ByteMessage = GetImageAsByteArray();

                                }
                            }

                            if (basicAuth)
                            {
                                response.AddBasicAuth();
                                //Console.WriteLine(response.GetHeadersAsString());
                                if (CheckAuthentication(response.GetHeadersAsString()))
                                {
                                    Console.WriteLine("Authorized.");
                                    ForwardResponse(callback, response);

                                    Console.WriteLine("Adding response to cache.");
                                    Console.WriteLine(request.GetRequestUrl());
                                    lock (CachedMessages) CachedMessages.Add(request.GetRequestUrl(), response);
                                }
                                else
                                {
                                    Console.WriteLine("Unauthorized.");
                                }
                            }
                            
                        }
                        catch (Exception e) when (
                            e is ArgumentNullException ||
                            e is ArgumentOutOfRangeException ||
                            e is SocketException ||
                            e is ObjectDisposedException
                        )
                        {
                            callback($"An Error has occurred: {e.Message}", Types.log);
                            Console.WriteLine("Error: " + e.Message);
                        }
                        finally
                        {
                            server.Close();
                        }

                    }
                }                
                
            }
            
            client.Close();
        }

        private byte[] GetImageAsByteArray()
        {
            Stream imgStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("ProxyApp.images.placeholder.png");

            var memoryStream = new MemoryStream();
            imgStream.CopyTo(memoryStream);

            return memoryStream.ToArray();
        }

        private Message ReceiveRequest(Action<string, Types> callback)
        {
            //http://www.yoda.arachsys.com/csharp/readbinary.html <-- place where I caught my example codes to handle stream reads properly.
            using (var mstrm = new MemoryStream())
            {
                var tempBuffer = new byte[1024];
                int bytesRead;
                if (clientStream.CanRead)
                {
                    if (clientStream.DataAvailable)
                    {
                        do
                        {
                            bytesRead = clientStream.Read(tempBuffer, 0, tempBuffer.Length);
                            mstrm.Write(tempBuffer, 0, bytesRead);

                        } while (clientStream.DataAvailable);
                    }
                }
                buffer = mstrm.GetBuffer();
                mstrm.Flush();
            }
            
            callback("Received Request: \n" + Encoding.ASCII.GetString(buffer, 0, buffer.Length), Types.request);

            Message messageObj = new Message(buffer);

            buffer = new byte[bufferSize];

            return messageObj;
        }

        private void ForwardRequest(TcpClient server, Action<string, Types> callback, Message request)
        {
            //TODO: Dit is echt heel lelijk, ik start deze stream liever ergens anders. Komt later.
            serverStream = server.GetStream();
            if (request != null)
            {
                byte[] message = request.GetMessageAsByteArray();
                callback("Forwarding request:\n" + request.GetHeadersAsString(), Types.log);
                serverStream.Write(message, 0, message.Length);
                serverStream.Flush();
            }
            else
            {
                //Change to callback function.
                callback("Requested site is not reachable.", Types.log);
            }
        }

        private Message ReceiveResponse(Action<string, Types> callback)
        {
            //bool responseReceived = false;
            //http://www.yoda.arachsys.com/csharp/readbinary.html
            using (var mstrm = new MemoryStream())
            {
                var tempBuffer = new byte[1024];
                int bytesRead;
                if (serverStream.CanRead)
                {
                    //TODO: Test of dit veranderd kan worden, zodat een tread.sleep niet meer nodig is.
                    //TODO: Test of Thread.sleep weg kan op de computer, doordat deze snellere connectie heeft (?)
                    //TODO: Check met een if of dit alleen op de allereerste request nodig is en dat het daarna snel genoeg is (?)
                    Thread.Sleep(500);

                    if (serverStream.DataAvailable)
                    {
                        do
                        {
                            bytesRead = serverStream.Read(tempBuffer, 0, tempBuffer.Length);
                            mstrm.Write(tempBuffer, 0, bytesRead);
                        } while (serverStream.DataAvailable);
                    }
                }

                buffer = mstrm.GetBuffer();
                mstrm.Flush();
            }
            
            callback("Response received: \n" + Encoding.ASCII.GetString(buffer, 0, buffer.Length), Types.response);

            Message messageObj = new Message(buffer);

            buffer = new byte[bufferSize];

            return messageObj;
        }

        private void ForwardResponse(Action<string, Types> callback, Message response)
        {
            
            if (response != null)
            {
                byte[] message = response.GetMessageAsByteArray();
                callback("Forwarding response:\n" + response.GetMessageAsLog(), Types.log);
                clientStream.Write(message, 0, message.Length);
                clientStream.Flush();
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
            string newUrl = "www.clipground.com/images/placeholder-clipart-6.jpg";
            headers[1] = newUrl;

            return newUrl;
        }

        private bool CheckAuthentication(string response)
        {
            string[] headers = response.Split('\n');
            foreach (string header in headers)
            {
                if (header.StartsWith("Proxy-Authorization: "))
                {
                    string username = "admin";
                    string password = "admin";
                    string encoded = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
                    string[] auth = header.Split(new[] { ": " }, StringSplitOptions.None);
                    foreach (string authRow in auth)
                    {
                        if (authRow.StartsWith("Basic "))
                            if(authRow.Equals("Basic " + encoded + "\r")) return true;
                    }
                    
                }
            }
            return false;
        }

        private void CheckForClearCache()
        {
            List<string> toBeRemovedItems = new List<string>();
            foreach(var Item in CachedMessages)
            {
                //Console.WriteLine("" + (Item.Value.Date + cacheDuration));
                //Console.WriteLine("" + DateTimeOffset.Now.ToUnixTimeMilliseconds());
                if(Item.Value.Date + cacheDuration < DateTimeOffset.Now.ToUnixTimeMilliseconds())
                {
                    toBeRemovedItems.Add(Item.Key);
                    //CachedMessages.Remove(Item.Key);
                    Console.WriteLine("Response removed from cache." + Item.Key);
                }
            }

            RemoveItemsFromCache(toBeRemovedItems);
        }

        private void RemoveItemsFromCache(List<string> items)
        {
            foreach(string item in items)
            {
                CachedMessages.Remove(item);
            }
        }
    }
}
