using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GameTTS_GUI
{
    static class Downloader
    {
        public static Window WindowContext { get; set; }

        public static WebClient Download(string uri, string filePath, ProgressBar progressBar)
        {
            WebClient client = null;
            using (client = new WebClient())
            {
                client.DownloadProgressChanged += (s, a) =>//PythonProgress;
                {
                    WindowContext.Dispatcher.Invoke(delegate
                    {
                        progressBar.Value = GetDLProgress(a);
                    });
                };
                //client.DownloadFileCompleted += PythonDownloadComplete;
                client.DownloadFileAsync(new Uri(uri), filePath);
            }

            return client;
        }

        public static void Install(string exePath, Action callback = null)
        {
            var task = Task.Factory.StartNew(() =>
            {
                using (Process process = Process.Start(new ProcessStartInfo(exePath)))
                {
                    process.WaitForExit();
                }
            });

            if(callback != null)
                task.ContinueWith(result => callback);
        }

        private static double GetDLProgress(DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;

            return int.Parse(Math.Truncate(percentage).ToString());
        }
    }
}
