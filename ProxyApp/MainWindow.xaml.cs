﻿using System;
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
        private bool listening = false;
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
                requestHandler = new RequestHandler(this, port, buffer);
                requestHandler.Listen((string message, RequestHandler.Types reponseType) =>
                {
                    switch(reponseType)
                    {
                        case RequestHandler.Types.request:
                        {
                            if (filterRequest) break;
                            else
                            {
                                AddToLog(message);
                                break;
                            }
                        }
                        case RequestHandler.Types.response:
                        {
                            if (filterResponse) break;
                            else
                            {
                                AddToLog(message);
                                break;
                            }
                        }
                        case RequestHandler.Types.log:
                        {
                            AddToLog(message);
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
                        }
                        AddToLog($"Changed buffer size to: {buffer}");
                    }
                }
            } 
        }

        private void ClearLogBtn_Click(object sender, RoutedEventArgs e)
        {
            RequestsList.Clear();
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
