using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ProxyApp
{
    class RequestHandler
    {
        MainWindow window;
        TcpListener listener;
        TcpClient client;
        byte[] buffer = new byte[1024];

        public RequestHandler(MainWindow window)
        {
            this.window = window;
        }

        public async void Listen()
        {
            listener = new TcpListener(IPAddress.Any, 8080);
            listener.Start();

            
            Console.WriteLine("Client found.");

            while(true)
            {
                client = await listener.AcceptTcpClientAsync();
                await Task.Run(() => HandleMessages());
            }
            //Convert client info to request info
            
        }

        private void HandleMessages()
        {
            string completeMessage = "";
            do
            {
                client.GetStream().Read(buffer, 0, buffer.Length);
                string message = Encoding.ASCII.GetString(buffer, 0, buffer.Length);
                completeMessage += message;
            } while (client.GetStream().DataAvailable);

            window.AddToLog(completeMessage);


            string url = GetUrlFromRequest(completeMessage);

            string requestSite = ReachSite(url);

            if(requestSite != "")
            {
                buffer = Encoding.ASCII.GetBytes(ReachSite(url));
                client.GetStream().Write(buffer, 0, buffer.Length);
            } else
            {
                window.AddToLog("Requested site is not reachable.");
            }
            client.Close();
        }

        private string ReachSite(string uri)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(uri);
            webRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            } catch(Exception e) when (
                e is WebException ||
                e is InvalidOperationException ||
                e is NotSupportedException ||
                e is ProtocolViolationException
            )
            {
                window.AddToLog("An error has occured.");
                Console.WriteLine("An error has occured: " + e.Message);
                return "";
            }
            
        }

        private string GetUrlFromRequest(string request)
        {
            string[] tokens = request.Split(' ');
            return tokens[1];
        }
    }
}
