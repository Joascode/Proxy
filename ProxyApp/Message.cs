using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ProxyApp
{
    class Message
    {
        //Store the message object returned by a request and response.

        byte[] message;
        WebHeaderCollection headers;
        StringBuilder sb = new StringBuilder();

        private static readonly string[] _imageExtensions = { "jpg", "bmp", "gif", "png", "jpeg" };

        public Message(WebHeaderCollection headers, byte[] message)
        {
            this.message = message;
            this.headers = headers;
        }

        public string GetMessageAsLog()
        {
            string body = Encoding.UTF8.GetString(message, 0, message.Length);

            sb.Append(GetHeadersAsString());
            sb.Append('\n');
            sb.Append(body);

            string fullMsg = sb.ToString();
            sb.Clear();

            return fullMsg;
        }

        public string GetETag()
        {
            return headers["ETag"];
        }

        public byte[] GetBody()
        {
            return message;
        }

        public string GetHeadersAsString()
        {
            foreach(string header in headers)
            {
                sb.AppendFormat("{0}:{1} \n", header, headers[header]);
            }

            var stringMsg = sb.ToString();
            sb.Clear();

            return stringMsg;
        }
    }
}
