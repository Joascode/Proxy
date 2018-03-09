using System;
using System.Collections.Generic;
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

        public RequestHandler(MainWindow window)
        {
            this.window = window;
        }

        public void Listen()
        {
            listener = new TcpListener(IPAddress.Any, 8080);
            listener.Start();

            TcpClient client = listener.AcceptTcpClient();

            //Convert client info to request info

        }
    }
}
