﻿using System;
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

        //public readonly string AUTHRESPONSE = "HTTP/1.1 401 Access Denied\r\nWWW-Authenticate: Basic realm = \"Proxy\"\r\nContent-Length: 0\r\n\r\n";
        private readonly string AUTHRESPONSE = "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"Proxy\"\r\nContent-Length: 0\r\n\r\n";
        private bool authenticated = false;

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
        public async void Listen(Action<Message, string, Types, string> callback)
        {
            //TODO: Make using so closing happens in here.
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            callback(null, null, Types.log, $"Listening on port: {port}");
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
        private void HandleMessages(Action<Message, string, Types, string> callback)
        {
            using (clientStream = client.GetStream())
            {
                Message request = ReceiveRequest(callback);
                Message response = null;

                //Check if request has been made earlier. If so, retrieve response tied to request.
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

                            //Check for changed content on Server.

                            if (basicAuth)
                            {
                                if (!authenticated)
                                {
                                    if(CheckAuthentication(request))
                                    {
                                        authenticated = true;
                                    }
                                    
                                    else
                                    {
                                        ReturnAuthorizationFailed(callback);
                                    }
                                }
                                else if(authenticated)
                                {
                                    if (removeBrowser) request.RemoveBrowserHeader("User-Agent");

                                    ForwardRequest(server, callback, request);

                                    response = ReceiveResponse(server, callback);

                                    if (blockImages)
                                    {
                                        if (response.RequestForImage())
                                        {
                                            Console.WriteLine("Asking for image.");
                                            response.ByteMessage = GetImageAsByteArray();

                                        }
                                    }

                                    ForwardResponse(callback, response);

                                    //TODO: Add last-modified to the request before placing into cache.


                                    Console.WriteLine("Adding response to cache.");
                                    Console.WriteLine(request.GetRequestUrl());
                                    lock (CachedMessages) CachedMessages.Add(request.GetRequestUrl(), response);
                                }

                                //De oude stijl van basic authentication.
                                /*response.AddBasicAuth();
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
                                }*/
                            }
                            
                        }
                        catch (Exception e) when (
                            e is ArgumentNullException ||
                            e is ArgumentOutOfRangeException ||
                            e is SocketException ||
                            e is ObjectDisposedException
                        )
                        {
                            callback(null, null, Types.log, $"An Error has occurred: {e.Message}");
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

        private void ReturnAuthorizationFailed(Action<Message, string, Types, string> callback)
        {
            byte[] resp = Encoding.ASCII.GetBytes(AUTHRESPONSE);
            Message response = new Message(resp);
            ForwardResponse(callback, response);
        }

        private byte[] GetImageAsByteArray()
        {
            Stream imgStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("ProxyApp.images.placeholder.png");

            var memoryStream = new MemoryStream();
            imgStream.CopyTo(memoryStream);

            return memoryStream.ToArray();
        }

        private Message ReceiveRequest(Action<Message, string, Types, string> callback)
        {
            //http://www.yoda.arachsys.com/csharp/readbinary.html <-- place where I caught my example codes to handle stream reads properly.
            using (var mstrm = new MemoryStream())
            {
                var tempBuffer = new byte[bufferSize];
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

            Message messageObj = new Message(buffer);

            callback(messageObj, "Received Request: \n", Types.request, null);

            buffer = new byte[bufferSize];

            return messageObj;
        }

        private void ForwardRequest(TcpClient server, Action<Message, string, Types, string> callback, Message request)
        {
            //TODO: Dit is echt heel lelijk, ik start deze stream liever ergens anders. Komt later.
            serverStream = server.GetStream();
            if (request != null)
            {
                byte[] message = request.ByteMessage;
                callback(request, "Forwarding request:\n", Types.request, null);
                serverStream.Write(message, 0, message.Length);
                serverStream.Flush();
            }
            else
            {
                callback(null, null, Types.log, "Requested site is not reachable.");
            }
        }

        private Message ReceiveResponse(TcpClient server, Action<Message, string, Types, string> callback)
        {
            //byte[] newBuffer;
            
            //http://www.yoda.arachsys.com/csharp/readbinary.html
            /*using (var mstrm = new MemoryStream())
            {
                
                var tempBuffer = new byte[1024];
                int bytesRead;
                if (stream.CanRead)
                {
                    stream.ReadTimeout = 100;
                    //TODO: Test of dit veranderd kan worden, zodat een tread.sleep niet meer nodig is.
                    //TODO: Test of Thread.sleep weg kan op de computer, doordat deze snellere connectie heeft (?)
                    //TODO: Check met een if of dit alleen op de allereerste request nodig is en dat het daarna snel genoeg is (?)
                    /*Thread.Sleep(500);
                    if(serverStream.DataAvailable)
                    {
                        do
                        {
                            bytesRead = serverStream.Read(tempBuffer, 0, tempBuffer.Length);
                            mstrm.Write(tempBuffer, 0, bytesRead);
                        } while (serverStream.DataAvailable);
                    }
                    

                    /*while ((bytesRead = stream.Read(tempBuffer, 0, tempBuffer.Length)) > 0)
                    {
                        Console.WriteLine(bytesRead);
                        mstrm.Write(tempBuffer, 0, bytesRead);
                    }
                }

                newBuffer = mstrm.ToArray();
                mstrm.Flush();
            }*/
            /*byte[] temp = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(temp, 0, temp.Length);
                    if (read <= 0)
                    { 
                        newBuffer = ms.ToArray();
                        break;
                    }
                    ms.Write(temp, 0, read);
                }
            }*/

            byte[] temp = new byte[bufferSize];
            int read = 0;

            int chunk;
            using (Stream stream = server.GetStream())
            try
            {
                stream.ReadTimeout = 1000;
                while ((chunk = stream.Read(temp, read, temp.Length - read)) > 0)
                {
                    read += chunk;

                    // If we've reached the end of our buffer, check to see if there's
                    // any more information
                    if (read == temp.Length)
                    {
                        int nextByte = stream.ReadByte();

                        // End of stream? If so, we're done
                        if (nextByte == -1)
                        {
                            break;
                        }

                        // Nope. Resize the buffer, put in the byte we've just
                        // read, and continue
                        byte[] newBuff = new byte[temp.Length * 2];
                        Array.Copy(temp, newBuff, temp.Length);
                        newBuff[read] = (byte)nextByte;
                        temp = newBuff;
                        read++;
                    }
                }
            }
            catch(IOException e)
            {
                Console.WriteLine("An error occurred: " + e.Message);
            }
            
            // Buffer is now too big. Shrink it.
            byte[] ret = new byte[read];
            Array.Copy(temp, ret, read);

            Message messageObj = new Message(ret);

            callback(messageObj, "Response received: \n", Types.response, null);

            return messageObj;
        }

        private void ForwardResponse(Action<Message, string, Types, string> callback, Message response)
        {
            
            if (response != null)
            {
                byte[] message = response.ByteMessage;
                callback(response, "Forwarding response:\n", Types.response, null);
                clientStream.Write(message, 0, message.Length);
                clientStream.Flush();
            }
            else
            {
                callback(null, null, Types.log, "Requested site is not reachable.");
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

        [Obsolete("ChangeImageRequest is deprecated, please use GetImageAsByteArray instead.", true)]
        private string ChangeImageRequest(string request)
        {
            string[] headers = request.Split(' ');
            string newUrl = "www.clipground.com/images/placeholder-clipart-6.jpg";
            headers[1] = newUrl;

            return newUrl;
        }

        [Obsolete("CheckAuthentication(string response) is deprecated, please use CheckAuthentication(Message request) instead.", true)]
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

        private bool CheckAuthentication(Message request)
        {
            string authHeader = request.GetAuthorizationHeader();

            if (authHeader != null)
            {
                string[] auth = authHeader.Split();

                string[] credentials = Encoding.UTF8.GetString(Convert.FromBase64String(auth[auth.Length - 1])).Split(':');

                if (credentials[0] == "admin" && credentials[1] == "admin")
                {

                    return true;

                }
            }
            return false;
        }

        private void CheckForClearCache()
        {
            List<string> toBeRemovedItems = new List<string>();
            foreach(var Item in CachedMessages)
            {
                if(Item.Value.Date + cacheDuration < DateTimeOffset.Now.ToUnixTimeMilliseconds())
                {
                    toBeRemovedItems.Add(Item.Key);
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
