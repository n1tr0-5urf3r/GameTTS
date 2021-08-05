using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json;

namespace GameTTS_GUI.Updater
{
    /// <summary>
    /// Static class to download and install dependencies.<br></br>
    /// Provides functionality to check for installed dependencies, download files
    /// and executing installers or command line instructions.
    /// </summary>
    static class DependencyManager
    {
        #region -- private properties

        private static WebClient currentDownload;
        private static GDriveDownloader currentGDriveDownload;
        private static Dictionary<string, InstallTask> installTasks = new Dictionary<string, InstallTask>();
        private static Task currentTask;

        #endregion

        #region -- events

        /// <summary>
        /// This event will be fired upon retrieving the json file containing
        /// dependency information (see <see cref="GetUpdate"/> for more info).
        /// </summary>
        public static event Action OnUpdateFileLoaded;

        /// <summary>
        /// This event will be fired after every queued dependency has been successfully installed.
        /// </summary>
        public static event Action OnUpdateFinished;

        #endregion

        #region -- public properties

        /// <summary>
        /// The update windows' dispatcher object, which is used to update several UI elements
        /// in the WPF thread.
        /// </summary>
        public static Dispatcher WindowDispatcher { get; set; }

        /// <summary>
        /// Shorthand property to check for a python installation (see <see cref="IsInstalled(Dependency)"/>).
        /// </summary>
        public static bool IsPythonInstalled => IsInstalled(Config.Get.Dependencies["python"]);

        /// <summary>
        /// Shorthand property to check for properly setup python dependencies and virtual environment 
        /// (see <see cref="IsInstalled(string, string, bool)"/>).
        /// </summary>
        public static bool IsPyDepInstalled => Directory.Exists(Config.VirtualEnvPath)
                && IsInstalled("pyDependencies");

        /// <summary>
        /// Shorthand property to check for a valid model file (see <see cref="IsInstalled(string, string, bool)"/> and 
        /// <see cref="CheckIntegrity(string, string)"/>).
        /// </summary>
        public static bool IsModelInstalled 
        {
            get
            {
                var dep = Config.Get.Dependencies["model"];
                return File.Exists(Config.ModelPath + dep.Name) && CheckIntegrity(Config.ModelPath + dep.Name, dep.Checksum); 
            }
        }

        #endregion

        /// <summary>
        /// Downloads the current update file from the server and stores it in the
        /// Config singleton's <see cref="Config.Dependencies"/>.<br/>
        /// Also fires <see cref="OnUpdateFileLoaded"/>.
        /// </summary>
        public static void GetUpdate()
        {
            using (var client = new WebClient())
            {
                client.DownloadFileCompleted += (sender, e) =>
                {
                    Config.Get.Dependencies = JsonConvert.DeserializeObject<Dictionary<string, Dependency>>(
                        File.ReadAllText(Config.UpdateFilePath));
                    OnUpdateFileLoaded();
                };
                client.DownloadFileAsync(new Uri(Config.Get.UpdateURL), Config.UpdateFilePath);
            }
        }

        /// <summary>
        /// Cancel any active download (normal or google drive).<br/>
        /// This should probably only be called on closing the whole application from
        /// within the updater, as <see cref="WebClient.DownloadFileCompleted"/> events might still be called!
        /// </summary>
        public static void CancelDownloads()
        {
            currentDownload?.CancelAsync();
            currentGDriveDownload?.Client.CancelAsync();
        }

        /// <summary>
        /// Add a dependency to the queue. Ensures that no duplicates are being queued.
        /// </summary>
        /// <param name="key">dependency key that's used throughout the application</param>
        /// <param name="task">stask object containing all necessary information</param>
        public static void QueueInstall(string key, InstallTask task)
        {
            if(!installTasks.ContainsKey(key))
            {
                installTasks.Add(key, task);
            }
        }

        /// <summary>
        /// This function will have a thread go through the queue and download and
        /// install every queued dependency. New items added to the queue while
        /// the thread is working should be fine, but there's should also be no
        /// occasion for this to actually happen. Anyway, if anything goes wrong,
        /// chances are it's somewhere within this function.
        /// </summary>
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
                            WindowDispatcher.Invoke(delegate
                            {
                                task.ProgressText.Text = "lade...";
                                task.ProgressText.Foreground = Brushes.Black;
                            });

                            var client = new WebClient();
                            using (client)
                            {
                                client.DownloadProgressChanged += (s, a) =>
                                {
                                    WindowDispatcher.Invoke(delegate
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
                        WindowDispatcher.Invoke(delegate
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
                        WindowDispatcher.Invoke(delegate
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

                    WindowDispatcher.Invoke(delegate
                    {
                        task.ProgressBar.IsIndeterminate = false;
                    });

                    installTasks.Remove(installTasks.Keys.ToArray()[0]);
                }
            }).ContinueWith(result => OnUpdateFinished());
        }

        /// <summary>
        /// Google Drive downloads need a special treatment, so this function starts
        /// a special download. See <see cref="GDriveDownloader"/> for more information.
        /// </summary>
        /// <param name="task">task object containing all necessary information</param>
        /// <param name="key">dependency key that's used throughout the application</param>
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
                    WindowDispatcher.Invoke(delegate
                    {
                        task.ProgressBar.Value = progress.ProgressPercentage;
                        task.ProgressText.Text = progress.ProgressPercentage.ToString() + "%";
                    });
                };
                currentGDriveDownload.DownloadFileCompleted += (s, a) => task.PostInstall?.Invoke();
                currentGDriveDownload.DownloadFileAsync(task.URL, task.FilePath);
            }
        }

        /// <summary>
        /// Returns a version string for an application with cmd/ps command.
        /// </summary>
        /// <param name="execName">the applications cmd/ps command</param>
        /// <returns>The version string in format "1.2.3"</returns>
        public static Version GetVersion(string execName)
        {
            string output = GetProgramOutput(execName);

            return ExtractVersion(output);
        }

        /// <summary>
        /// Runs a SHA1 comparison (currently via output string) for a given file.<br/>
        /// This might cause some lag for larger files!
        /// </summary>
        /// <param name="filePath">the file location to check</param>
        /// <param name="checksum">the checksum to test against</param>
        /// <returns></returns>
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

            return hashString.Equals(checksum, StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Checks if a given program is installed correctly.<br/>
        /// It will check the output of it's cmd/ps command for any version string
        /// and compare it to the version provided by the argument object.
        /// </summary>
        /// <param name="dep"></param>
        /// <returns></returns>
        public static bool IsInstalled(Dependency dep)
        {
            string output = GetProgramOutput(dep.Name);

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

        /// <summary>
        /// Checks for existing path and file integrity via SHA1 comparison (<see cref="CheckIntegrity(string, string)"/>) and version.
        /// </summary>
        /// <param name="key">dependency key that's used throughout the application</param>
        /// <param name="path">file or directory path</param>
        /// <param name="integrityCheck"><c>true</c> if a SHA1 check should be performed. Default is <c>false</c></param>
        /// <returns></returns>
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

        /// <summary>
        /// Runs a command on cmd for --version check and returns it's output.
        /// </summary>
        /// <param name="execName">the applications cmd command</param>
        /// <returns></returns>
        public static string GetProgramOutput(string execName)
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

        /// <summary>
        /// Extracts a version string from the given input with regex.
        /// </summary>
        /// <param name="versionString">possible the output of a cmd command version check</param>
        /// <returns></returns>
        public static Version ExtractVersion(string versionString)
        {
            Regex pattern = new Regex(@"\d+(\.\d+)+");
            Match m = pattern.Match(versionString);
            string version = m.Value;

            return new Version(m.Value);
        }

        /// <summary>
        /// Simple helper method to convert download progress into human readable percentage.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private static double GetDLProgress(DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;

            return int.Parse(Math.Truncate(percentage).ToString());
        }

        /// <summary>
        /// Helper class to group all necessery information for downloading and installing dependencies.
        /// </summary>
        public class InstallTask
        {
            /// <summary>
            /// File URL to download.
            /// </summary>
            public string URL { get; set; }
            /// <summary>
            /// Path (incl. file name) to download to.
            /// </summary>
            public string FilePath { get; set; }
            /// <summary>
            /// Currently only used for install script to define the resulting 
            /// .venv path to be checked for.
            /// </summary>
            public string TargetDirectory { get; set; }
            /// <summary>
            /// Callback to be fired after the installation finished.
            /// </summary>
            public Func<bool> PostInstall { get; set; }
            /// <summary>
            /// Callback to be fired before an installation is taking place.
            /// </summary>
            public Action PreInstall { get; set; }
            /// <summary>
            /// WPF object reference for manipulation via Dispatcher (e.g. in downloads)
            /// </summary>
            public ProgressBar ProgressBar { get; set; }
            /// <summary>
            /// WPF object reference for manipulation via Dispatcher (e.g. in downloads)
            /// </summary>
            public TextBlock ProgressText { get; set; }
        }
    }
}
