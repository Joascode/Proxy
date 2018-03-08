using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProxyApp
{
    class RequestsList
    {
        public ObservableCollection<string> Requests;

        public RequestsList()
        {
            Requests = new ObservableCollection<string>
            {
                "Listening for requests"
            };
        }

        public void AddRequest(string request)
        {
            Requests.Add(request);
        }
    }
}
