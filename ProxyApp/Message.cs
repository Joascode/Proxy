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

        byte[] byteMessage;
        //string[] strMessage;
        StringBuilder sb = new StringBuilder();

        private static readonly string[] _imageExtensions = { "jpg", "bmp", "gif", "png", "jpeg" };

        public Message(byte[] byteMessage)
        {
            this.byteMessage = byteMessage;
            //this.strMessage = strMessage;
        }

        public string GetMessageAsLog()
        {
            string body = Encoding.UTF8.GetString(byteMessage, 0, byteMessage.Length);

            //sb.Append(GetHeadersAsString());
            //sb.Append('\n');
            //sb.Append(body);

            //string fullMsg = sb.ToString();
            //sb.Clear();

            return body;
        }

        /*public string GetETag()
        {
            return headers["ETag"];
        }*/

        public byte[] GetBody()
        {
            return byteMessage;
        }

        /*public string GetHeadersAsString()
        {
            foreach(string header in headers)
            {
                sb.AppendFormat("{0}:{1} \n", header, headers[header]);
            }

            var stringMsg = sb.ToString();
            sb.Clear();

            return stringMsg;
        }*/

        public string GetHeadersAsString()
        {
            string message = Encoding.ASCII.GetString(byteMessage, 0, byteMessage.Length);

            string[] splitMessage = message.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None);

            return splitMessage[0];
        }
    }
}
