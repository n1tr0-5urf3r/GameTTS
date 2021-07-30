using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;

namespace GameTTS_GUI.Updater
{
    static class Dependencies
    {
        public static UpdateWindow WindowContext { get; set; }

        public static readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
        public static WebClient CurrentDownload { get; private set; }
        public static GDriveDownloader CurrentGDriveDownload { get; private set; }

        public static event Action<bool> OnConnectionCheck;
        public static event Action OnUpdateFileLoaded;
        public static event Action OnUpdateFinished;

        public static Timer ConnectionChecker { get; private set; }

        public static void GetUpdate()
        {
            //download json from server
            string path = @"Resources/json/update.json";
            var client = new WebClient();
            client.DownloadFileCompleted += (sender, e) =>
            {
                Config.Get.Dependencies = JsonConvert.DeserializeObject<Dictionary<string, Dependency>>(
                    File.ReadAllText(path));
                OnUpdateFileLoaded();
            };
            Download(ref client, Config.Get.UpdateURL, path, null);
        }

        public static void InstallAll(List<InstallTask> tasks)
        {
            Task.Factory.StartNew(() =>
            {
                if (!Directory.Exists(@"GameTTS\tmp"))
                    Directory.CreateDirectory(@"GameTTS\tmp");

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
                                CurrentDownload = client;
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
                            task.LoadingLabel.Text = "installiere...";
                            task.LoadingLabel.Foreground = Brushes.Black;
                        });
                    }

                    //start install
                    task.PreInstall?.Invoke();
                    if (task.FilePath.EndsWith(".ps1"))
                    {
                        WindowContext.Dispatcher.Invoke(delegate
                        {
                            task.ProgressBar.IsIndeterminate = true;
                        });

                        ProcessStartInfo processInfo = new ProcessStartInfo()
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy unrestricted \"{task.FilePath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (Process process = Process.Start(processInfo))
                        {
                            process.WaitForExit();
                        }
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
                        task.ProgressBar.IsIndeterminate = false;
                    });
                }
            }).ContinueWith(result => OnUpdateFinished());
        }

        public static void Download(ref WebClient client, string url, string filePath, ProgressBar progressBar)
        {
            using (client)
            {
                if (progressBar != null)
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
            if (!Directory.Exists(@"GameTTS\tmp"))
                Directory.CreateDirectory(@"GameTTS\tmp");

            using (gdl = new GDriveDownloader())
            {
                gdl.DownloadProgressChanged += (s, progress) =>
                {
                    WindowContext.Dispatcher.Invoke(delegate
                    {
                        progressBar.Value = progress.ProgressPercentage;
                    });
                };
                CurrentGDriveDownload = gdl;
                gdl.DownloadFileAsync(url, filePath);
            }
        }

        public static void RunConnectionWatcher(long checkIntervalMillis)
        {
            if (ConnectionChecker != null)
                return;

            ConnectionChecker = new Timer((a) =>
            {
                OnConnectionCheck(CheckConnection());
            }, null, 0, checkIntervalMillis);
        }

        public static Version GetVersion(string execName)
        {
            string output = GetProgramVersionOutput(execName);

            return ExtractVersion(output);
        }

        public static bool IsInstalled(Dependency dep)
        {
            string output = GetProgramVersionOutput(dep.Name);

            if (output == null)
                return false;

            //this line also starty with "Python ", so we'd have a false positive here
            if (output.Contains("was not found"))
                return false;

            var version = ExtractVersion(output);

            if (version.Major < dep.Version.Major)
                return false;

            if (version.Minor < dep.Version.Minor)
                return false;

            return true;
        }

        public static bool IsInstalled(string key)
        {
            var dep = Config.Get.Dependencies[key];

            if (Config.Get.FileVersions[key] < dep.Version.Major)
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

        public static Version ExtractVersion(string versionString)
        {
            Regex pattern = new Regex(@"\d+(\.\d+)+");
            Match m = pattern.Match(versionString);
            string version = m.Value;

            return new Version(m.Value);
        }

        public static double GetDLProgress(DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;

            return int.Parse(Math.Truncate(percentage).ToString());
        }

        /// <summary>
        /// Sends a ping to Google to check internet connectin. Returns true if ping was successful.
        /// </summary>
        /// <returns></returns>
        public static bool CheckConnection()
        {
            Ping myPing = new Ping();
            string host = "google.com";
            byte[] buffer = new byte[32];
            int timeout = 2000;
            PingOptions pingOptions = new PingOptions();

            try
            {
                PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);

                if (reply.Status == IPStatus.Success)
                    return true;
            }
            catch (PingException) { }

            return false;
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
