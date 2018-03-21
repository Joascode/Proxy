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
        private byte[] byteMessage;
        public byte[] ByteMessage
        {
            get
            {
                return byteMessage;
            }
            set
            {
                byteMessage = value;
            }
        }
        //private bool messageAdjusted = false;
        private StringBuilder sb = new StringBuilder();
        private string stringMessage;
        private long date = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        public long Date
        {
            get
            {
                return date;
            }
        }

        private static readonly string[] _imageExtensions = { "jpg", "bmp", "gif", "png", "jpeg" };

        public Message(byte[] byteMessage)
        {
            this.byteMessage = byteMessage;
        }

        public string GetMessageAsLog()
        {
            return Encoding.UTF8.GetString(byteMessage, 0, byteMessage.Length);
        }

        public byte[] GetMessage()
        {
            return byteMessage;
        }

        public void RemoveBrowserHeader(string headerName)
        {
            string headersString = GetHeadersAsString();
            string[] headers = headersString.Split('\n');
            foreach(string header in headers)
            {
                if (!header.StartsWith($"{headerName}:"))
                {
                    //Console.WriteLine("Adding header: " + header);
                    sb.Append(header);
                }
            }
            string newMessage = sb.ToString();
            sb.Clear();

            //messageAdjusted = true;

            stringMessage = newMessage;
        }

        public bool RequestForImage()
        {
            string headersString = GetHeadersAsString();
            string[] headers = headersString.Split('\n');
            foreach (string header in headers)
            {
                if (header.StartsWith("Content-Type:"))
                {
                    foreach (string imageType in _imageExtensions)
                    {
                        if (header.EndsWith(imageType+"\r"))
                        {
                            Console.WriteLine("Image found!");
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        //Deze is af.
        public string GetHostFromRequest()
        {
            string stringHeader = GetHeadersAsString();
            string[] headers = stringHeader.Split('\n');
            foreach(string header in headers)
            {
                if(header.StartsWith("Host:"))
                {
                    string[] host = header.Split(' ');
                    //foreach (string hosti in host) Console.WriteLine(hosti);
                    return host[1].Split('\r')[0];
                }
            }
            return null;
        }

        public string GetRequestUrl()
        {
            string stringHeader = GetHeadersAsString();
            string[] headers = stringHeader.Split(' ');
            foreach (string header in headers)
            {
                if (header.StartsWith("https:") || header.StartsWith("http:"))
                {
                    return header;
                }
            }
            return null;
        }

        //Deze is af.
        public string GetHeadersAsString()
        {
            if(stringMessage == null)
            {
                return GetMessageAsStringArray()[0];
            }
            return stringMessage;
            
        }

        public string GetBodyAsString()
        {
            if(GetMessageAsStringArray()[1] != null)
            {
                return GetMessageAsStringArray()[1];
            }
            return null;
        }

        public void AddBasicAuth()
        {
            string headers = GetHeadersAsString();
            string username = "admin";
            string password = "admin";
            string encoded = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(username + ":" + password));
            string[] splitMessage = headers.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None);
            sb.Append(splitMessage[0]);
            sb.Append("\n");
            sb.Append("Proxy-Authorization: Basic " + encoded);
            sb.Append("\r");
            sb.Append("\n");

            string newHeaders = sb.ToString();
            //Console.WriteLine(newHeaders);
            sb.Clear();

            //messageAdjusted = true;

            stringMessage = newHeaders;
        }

        public byte[] GetMessageAsByteArray()
        {
            /*if(messageAdjusted)
            {
                Console.WriteLine(stringMessage);
                string message = stringMessage + "\n" + GetBodyAsString();
                messageAdjusted = false;
                return Encoding.ASCII.GetBytes(message);
            } else
            {*/
                return byteMessage;
            //}
        }

        private string[] GetMessageAsStringArray()
        {
            //string message = Convert.ToBase64String(byteMessage, Base64FormattingOptions.InsertLineBreaks);
            string message = Encoding.ASCII.GetString(byteMessage, 0, byteMessage.Length);
            //Console.WriteLine(message);
            string[] splitMessage = message.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.None);

            return splitMessage;
        }

        private string GetLastModified()
        {
            string[] headers = GetMessageAsStringArray();
            foreach(string header in headers)
            {
                if(header.StartsWith("Last-Modified"))
                {
                    string[] lastModifiedHeader = header.Split(' ');
                    return lastModifiedHeader[1];
                }
            }
            return null;
        }

        private void SetIfModifiedSince(string value)
        {
            string headers = GetHeadersAsString();

            sb.Append(headers);
            sb.Append("If-Modified-Since: " + value);
            sb.Append("\r");
            sb.Append("\n");

            string newHeaders = sb.ToString();
            Console.WriteLine(newHeaders);
            sb.Clear();

            //messageAdjusted = true;

            stringMessage = newHeaders;
        }
    }
}
