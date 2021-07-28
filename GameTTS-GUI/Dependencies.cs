using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameTTS_GUI
{
    static class Dependencies
    {
        public static UpdateWindow WindowContext { get; set; }

        private static Dictionary<string, InstallTask> toInstall = new Dictionary<string, InstallTask>();

        public static readonly CancellationTokenSource Cancellation = new CancellationTokenSource();

        public static void InstallAll(List<InstallTask> tasks)
        {
            Task.Factory.StartNew(() =>
            {
                foreach (var task in tasks)
                {
                    if (!File.Exists(task.FilePath))
                    {
                        if (task.URL != null)
                        {
                            WindowContext.Dispatcher.Invoke(delegate
                            {
                                task.LoadingLabel.Text = "lade...";
                                task.LoadingLabel.Foreground = Brushes.Black;
                            });

                            var client = new WebClient();
                            using (client)
                            {
                                client.DownloadProgressChanged += (s, a) =>
                                {
                                    WindowContext.Dispatcher.Invoke(delegate
                                    {
                                        task.ProgressBar.Value = GetDLProgress(a);
                                    });
                                };
                                client.DownloadFileAsync(new Uri(task.URL), task.FilePath);
                            }

                            while (client.IsBusy)
                            {
                                //wait
                            }
                        }
                    }
                    else
                    {
                        WindowContext.Dispatcher.Invoke(delegate
                        {
                            task.ProgressBar.Value = 100;
                        });
                    }

                    //start install
                    task.PreInstall?.Invoke();
                    if (task.FilePath.EndsWith(".ps1"))
                    {

                    }
                    else
                    {
                        using (Process process = Process.Start(new ProcessStartInfo(task.FilePath)))
                        {
                            process.WaitForExit();
                        }
                    }
                    //cancel?
                    if (task.PostInstall != null ? !task.PostInstall.Invoke() : false)
                        return;

                    WindowContext.Dispatcher.Invoke(delegate
                    {
                        task.ProgressBar.Foreground = Brushes.Green;
                    });
                }
            });
        }

        public static void Download(ref WebClient client, string url, string filePath, ProgressBar progressBar)
        {
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
        }

        public static void DownloadGDrive(ref GDriveDownloader gdl, string url, string filePath, ProgressBar progressBar)
        {
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
        }

        public static void Install(string path, Action preInstall = null, Action postInstall = null)
        {
            Task.Factory.StartNew(() =>
            {
                foreach (var task in toInstall.Values)
                {
                    preInstall?.Invoke();
                    using (Process process = Process.Start(new ProcessStartInfo(path)))
                    {
                        process.WaitForExit();
                    }
                    postInstall?.Invoke();
                }
            });
        }

        public static string GetVersion(string execName, string versionPrefix)
        {
            string output = GetProgramVersionOutput(execName);

            output = output.Replace(versionPrefix, "").TrimStart(' ');

            if (output.Contains(" "))
                output = output.Substring(0, output.IndexOf(' '));

            return output;
        }

        public static bool IsInstalled(string execName, string versionPrefix, int? major = null, int? minor = null)
        {
            string output = GetProgramVersionOutput(execName);

            if (output == null)
                return false;

            //this line also starty with "Python ", so we'd have a false positive here
            if (output.Contains("was not found"))
                return false;

            var version = Version.Parse(GetVersion(execName, versionPrefix));

            if (output.Contains(versionPrefix))
            {
                if (major != null)
                    if (version.Major < major.Value)
                        return false;

                if (minor != null)
                    if (version.Minor < minor.Value)
                        return false;
            }
            else
                return false;

            return true;
        }

        public static string GetProgramVersionOutput(string execName)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo()
            {
                FileName = execName,
                Arguments = "--version",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            string output = null;

            try
            {
                using (Process process = Process.Start(processInfo))
                {
                    using (StreamReader myStreamReader = process.StandardOutput)
                    {
                        output = myStreamReader.ReadLine();
                        process.WaitForExit();
                    }
                }
            }
            catch (Win32Exception)
            {
                //file not found, can return null
                return null;
            }

            return output;
        }

        public static double GetDLProgress(DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;

            return int.Parse(Math.Truncate(percentage).ToString());
        }

        public class InstallTask
        {
            public string URL { get; set; }
            public string FilePath { get; set; }
            public Func<bool> PostInstall { get; set; }
            public Action PreInstall { get; set; }
            public ProgressBar ProgressBar { get; set; }
            public TextBlock LoadingLabel { get; set; }
        }
    }
}
