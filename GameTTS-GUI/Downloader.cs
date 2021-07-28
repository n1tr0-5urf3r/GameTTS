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

        private static Queue<InstallTask> installTasks = new Queue<InstallTask>();
        private static Task currentTask;
        private static object threadLock = new object();

        public static WebClient Download(string url, string filePath, ProgressBar progressBar, bool useGDrive = false)
        {
            if (useGDrive)
            {
                GDriveDownloader gdl = null;
                using (gdl = new GDriveDownloader())
                {
                    gdl.DownloadProgressChanged += (s, progress) =>
                    {
                        WindowContext.Dispatcher.Invoke(delegate
                        {
                            progressBar.Value = progress.ProgressPercentage;
                        });
                    };
                    gdl.DownloadFileAsync(url, filePath);
                }

                return gdl.Client;
            }
            else
            {
                var client = new WebClient();
                using (client = new WebClient())
                {
                    client.DownloadProgressChanged += (s, a) =>
                    {
                        WindowContext.Dispatcher.Invoke(delegate
                        {
                            progressBar.Value = GetDLProgress(a);
                        });
                    };
                    client.DownloadFileAsync(new Uri(url), filePath);
                }

                return client;
            }
        }

        public static void QueueInstall(string exePath, Action postInstall = null, Action preInstall = null)
        {
            var task = new InstallTask { Path = exePath, PostInstall = postInstall, PreInstall = preInstall };
            task.PostInstall += Install; //after finishing install, check the next in queue
            installTasks.Enqueue(task);
            Install();
        }

        private static void Install()
        {
            lock (threadLock)
            {
                if (currentTask == null || currentTask.IsCompleted)
                    if (installTasks.Count > 0)
                    {
                        var task = installTasks.Dequeue();

                        task.PreInstall?.Invoke();

                        currentTask = Task.Factory.StartNew(() =>
                        {
                            using (Process process = Process.Start(new ProcessStartInfo(task.Path)))
                            {
                                process.WaitForExit();
                            }
                        });

                        //if (task.Callback != null)
                        currentTask.ContinueWith(result => task.PostInstall?.Invoke());//.ContinueWith(result => Install());
                    }
            }
        }

        private static double GetDLProgress(DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;

            return int.Parse(Math.Truncate(percentage).ToString());
        }

        private class InstallTask
        {
            public string Path { get; set; }
            public Action PostInstall { get; set; }
            public Action PreInstall { get; set; }
        }
    }
}
