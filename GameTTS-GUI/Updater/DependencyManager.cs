using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
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
    static class DependencyManager
    {
        private static WebClient currentDownload;
        private static GDriveDownloader currentGDriveDownload;
        private static Dictionary<string, InstallTask> installTasks = new Dictionary<string, InstallTask>();
        private static Task currentTask;

        public static event Action OnUpdateFileLoaded;
        public static event Action OnUpdateFinished;

        #region -- public properties

        public static UpdateWindow WindowContext { get; set; }

        public static bool IsPythonInstalled => IsInstalled(Config.Get.Dependencies["python"]);
        public static bool IsPyDepInstalled => Directory.Exists(Config.VirtualEnvPath)
                && IsInstalled("pyDependencies");
        public static bool IsModelInstalled => File.Exists(Config.ModelPath + Config.Get.Dependencies["model"].Name)
                && IsInstalled("model", Config.ModelPath + Config.Get.Dependencies["model"].Name, true);

        #endregion

        public static void GetUpdate()
        {
            //download json from server
            var client = new WebClient();
            client.DownloadFileCompleted += (sender, e) =>
            {
                Config.Get.Dependencies = JsonConvert.DeserializeObject<Dictionary<string, Dependency>>(
                    File.ReadAllText(Config.UpdateFilePath));
                OnUpdateFileLoaded();
            };
            Download(ref client, Config.Get.UpdateURL, Config.UpdateFilePath, null);
        }

        public static void CancelDownloads()
        {
            currentDownload?.CancelAsync();
            currentGDriveDownload?.Client.CancelAsync();
        }

        public static void QueueInstall(string key, InstallTask task)
        {
            if(!installTasks.ContainsKey(key))
            {
                installTasks.Add(key, task);
            }
        }

        public static void InstallAll()
        {
            if (currentTask != null)
                return;

            currentTask = Task.Factory.StartNew(() =>
            {
                if (!Directory.Exists(Config.TempPath))
                    Directory.CreateDirectory(Config.TempPath);

                while(installTasks.Count > 0)
                {
                    string key = installTasks.Keys.ToArray()[0];
                    var task = installTasks[key];

                    if (!File.Exists(task.FilePath))
                    {
                        if (task.URL != null)
                        {
                            WindowContext.Dispatcher.Invoke(delegate
                            {
                                task.ProgressText.Text = "lade...";
                                task.ProgressText.Foreground = Brushes.Black;
                            });

                            var client = new WebClient();
                            using (client)
                            {
                                client.DownloadProgressChanged += (s, a) =>
                                {
                                    WindowContext.Dispatcher.Invoke(delegate
                                    {
                                        double progress = GetDLProgress(a);
                                        task.ProgressBar.Value = progress;
                                        task.ProgressText.Text = ((int)progress).ToString() + "%";
                                    });
                                };
                                currentDownload = client;
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
                            task.ProgressText.Text = "installiere...";
                            task.ProgressText.Foreground = Brushes.Black;
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
                        string fullPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\" + task.FilePath;
                        using (Process process = Process.Start(new ProcessStartInfo(fullPath)))
                        {
                            process.WaitForExit();
                        }
                    }
                    //cancel?
                    if (task.PostInstall != null ? !task.PostInstall.Invoke() : false)
                        return;

                    WindowContext.Dispatcher.Invoke(delegate
                    {
                        task.ProgressBar.IsIndeterminate = false;
                    });

                    installTasks.Remove(installTasks.Keys.ToArray()[0]);
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

        public static void DownloadGDrive(InstallTask task, string key = null)
        {
            if (!Directory.Exists(Config.TempPath))
                Directory.CreateDirectory(Config.TempPath);

            if(key != null
            && File.Exists(task.FilePath))
                if(CheckIntegrity(task.FilePath, Config.Get.Dependencies[key].Checksum))
                {
                    task.PostInstall?.Invoke();
                    return;
                }

            using (currentGDriveDownload = new GDriveDownloader())
            {
                currentGDriveDownload.DownloadProgressChanged += (s, progress) =>
                {
                    WindowContext.Dispatcher.Invoke(delegate
                    {
                        task.ProgressBar.Value = progress.ProgressPercentage;
                        task.ProgressText.Text = progress.ProgressPercentage.ToString() + "%";
                    });
                };
                currentGDriveDownload.DownloadFileCompleted += (s, a) => task.PostInstall?.Invoke();
                currentGDriveDownload.DownloadFileAsync(task.URL, task.FilePath);
            }
        }

        public static Version GetVersion(string execName)
        {
            string output = GetProgramVersionOutput(execName);

            return ExtractVersion(output);
        }

        public static bool CheckIntegrity(string filePath, string checksum)
        {
            string hashString = "";
            using (var sha1 = SHA1.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha1.ComputeHash(stream);
                    hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }

            return hashString.Equals(checksum);
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

        public static bool IsInstalled(string key, string path = null, bool integrityCheck = false)
        {
            if(path != null)
            {
                try
                {
                    if (!File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                        if (integrityCheck)
                            if (!CheckIntegrity(path, Config.Get.Dependencies[key].Checksum))
                                return false;
                }
                catch (Exception) { return false; }
            }

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

        public class InstallTask
        {
            public string URL { get; set; }
            public string FilePath { get; set; }
            public string TargetDirectory { get; set; }
            public Func<bool> PostInstall { get; set; }
            public Action PreInstall { get; set; }
            public ProgressBar ProgressBar { get; set; }
            public TextBlock ProgressText { get; set; }
        }
    }
}
