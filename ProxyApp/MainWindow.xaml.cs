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
        private RequestHandler requestHandler;
        public ObservableCollection<string> RequestsList { get; set; }
        private int port = 8080;
        private int buffer = 1024;
        private int cache = 30000;
        private bool listening = false;
        private bool filterRequestHeaders = false;
        private bool filterResponseHeaders = false;
        private bool filterRequest = false;
        private bool filterResponse = false;

        public MainWindow()
        {
            RequestsList = new ObservableCollection<string>();
            InitializeComponent();
            DataContext = this;
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            if(listening)
            {
                listening = false;
                requestHandler.Close();
                requestHandler = null;
                AddToLog("Closing the listener.");
            } else
            {
                listening = true;
                requestHandler = new RequestHandler(port, buffer);
                //TODO: Fix callback to filter out body or headers based on settings.
                //TODO: Fix callback to work without Types.
                //TODO: Fix callback to work with errors properly.
                //TODO: Work with optional parameters.
                requestHandler.Listen((Message message, string messagePreFix, RequestHandler.Types reponseType, string log) =>
                {
                    switch(reponseType)
                    {
                        case RequestHandler.Types.request:
                        {
                            if (filterRequest) break;
                            else
                            {
                                if(filterRequestHeaders)
                                {
                                    AddToLog(messagePreFix + " " + message.GetBodyAsString());
                                }
                                else
                                {
                                    AddToLog(messagePreFix + " " + message.GetMessageAsLog());
                                }
                                break;
                            }
                        }
                        case RequestHandler.Types.response:
                        {
                            if (filterResponse) break;
                            else
                            {
                                if(filterResponseHeaders)
                                {
                                    AddToLog(messagePreFix + " " + message.GetBodyAsString());
                                }
                                else
                                {
                                    AddToLog(messagePreFix + " " + message.GetMessageAsLog());
                                }
                                break;
                            }
                        }
                        case RequestHandler.Types.log:
                        {
                            AddToLog(log);
                            break;
                        }
                    }
                });
                AddToLog("Listening for requests.");
            }
            
            ListenBtn.Content = listening ? "Stop" : "Start";
        }

        private void PortChangedHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (int.TryParse(PortNumberTxt.Text, out port))
                {
                    if(port <= 0)
                    {
                        AddToLog("Please enter a positive port number.");
                    } else
                    {
                        if (requestHandler == null) AddToLog("Can't change port while listening.");
                        else
                        {
                            requestHandler.Port = port;
                            AddToLog($"Changed port to: {port}");
                        }
                    }
                }
            }
        }

        private void BufferSizeChangedHandler(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Return)
            {
                if (int.TryParse(BufferSizeTxt.Text, out buffer))
                {
                    if (buffer <= 0)
                    {
                        AddToLog("Please enter a positive buffer size.");
                    }
                    else
                    {
                        if (requestHandler != null)
                        {
                            requestHandler.BufferSize = buffer;
                            AddToLog($"Changed buffer size to: {buffer}");
                        }
                        else
                        {
                            AddToLog("Please start the proxy first.");
                        }
                        
                    }
                }
            } 
        }

        private void CacheDurationHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (int.TryParse(CacheDurationTxt.Text, out cache))
                {
                    if (cache <= 0)
                    {
                        AddToLog("Please enter a positive cache duration.");
                    }
                    else
                    {
                        if (requestHandler != null)
                        {
                            requestHandler.CacheDuration = cache;
                            AddToLog($"Changed cache duration to: {buffer}");
                        }
                        else
                        {
                            AddToLog("Please start the proxy first.");
                        }
                        
                    }
                }
            }
        }

        private void ClearLogBtn_Click(object sender, RoutedEventArgs e)
        {
            RequestsList.Clear();

            ClearLogBtn.IsEnabled = false;
        }

        private void ChangeContentCheck_Click(object sender, RoutedEventArgs e)
        {
            ChangeContentCheckBox.IsChecked = false;
            AddToLog("This function isn't required, see requirements.");
        }

        private void RequestHeadersCheck_Click(object sender, RoutedEventArgs e)
        {
            filterRequestHeaders = (bool)RequestHeadersCheckBox.IsChecked;
            AddToLog("Filtering out the Request Headers.");
        }

        private void ResponseHeadersCheck_Click(object sender, RoutedEventArgs e)
        {
            filterResponseHeaders = (bool)ResponseHeadersCheckBox.IsChecked;
            AddToLog("Filtering out the Response Headers.");
        }

        private void ContentInCheck_Click(object sender, RoutedEventArgs e)
        {
            filterRequest = (bool) ContentInCheckBox.IsChecked;
            AddToLog("Filtering out the Requests.");
        }

        private void ContentUitCheck_Click(object sender, RoutedEventArgs e)
        {
            filterResponse = (bool)ContentUitCheckBox.IsChecked;
            AddToLog("Filtering out the Responses.");
        }

        private void HeaderEditCheck_Click(object sender, RoutedEventArgs e)
        {
            //Catch error when proxy isn't listening yet.
            if(requestHandler == null)
            {
                AddToLog("Please start the proxy first.");
                HeaderEditCheckBox.IsChecked = false;
            } else
            {
                requestHandler.RemoveBrowser = (bool)HeaderEditCheckBox.IsChecked;
                AddToLog("Deleting User-Agent header.");
            }
            
        }

        private void ContentFilterCheck_Click(object sender, RoutedEventArgs e)
        {
            if (requestHandler == null)
            {
                AddToLog("Please start the proxy first.");
                ContentFilterCheckBox.IsChecked = false;
            }
            else
            {
                requestHandler.BlockImages = (bool)ContentFilterCheckBox.IsChecked;

                if ((bool)ContentFilterCheckBox.IsChecked) AddToLog("Replacing images with placeholder.");
                else AddToLog("Allowing images.");
            }
        }

        private void BasicAuthCheck_Click(object sender, RoutedEventArgs e)
        {
            if(requestHandler == null)
            {
                AddToLog("Please start the proxy first.");
                BasicAuthCheckBox.IsChecked = false;
            }
            else
            {
                requestHandler.basicAuth = (bool)BasicAuthCheckBox.IsChecked;

                if ((bool)BasicAuthCheckBox.IsChecked) AddToLog("Checking for Authentication");
                else AddToLog("Blocking unauthicated user.");
            }
        }

        private void ClientCheck_Click(object sender, RoutedEventArgs e)
        {
            AddToLog("I got no clue what this is supposed to do.");
            ClientCheckBox.IsChecked = false;
        }

        private void AddToLog(string request)
        {
            if(!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => AddToLog(request));
                return;
            }

            RequestsList.Add(request);

            ClearLogBtn.IsEnabled = true;
            
        }
    }
}
