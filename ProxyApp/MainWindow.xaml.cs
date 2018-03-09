using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ProxyApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private RequestHandler RequestHandler;
        public ObservableCollection<string> RequestsList { get; set; }
        //public ObservableCollection<Request> Requests { get; set; }

        public MainWindow()
        {
            RequestsList = new ObservableCollection<string>();
            InitializeComponent();
            DataContext = this;
            //RequestsList.AddRequest("Listening");
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            RequestHandler = new RequestHandler(this);
            //Console.WriteLine("Starting the server.");
            RequestHandler.Listen();
            AddToLog("Listening for requests.");
        }

        public void AddToLog(string request)
        {
            if(!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AddToLog(request));
                return;
            }
            RequestsList.Add(request);
        }
    }
}
