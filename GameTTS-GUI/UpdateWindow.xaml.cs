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
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Newtonsoft.Json;

namespace GameTTS_GUI
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : Window
    {
        public bool CanDownloadPython { get; private set; }
        public bool CanDownloadEspeak { get; private set; }
        public bool CanDownloadDependencies { get; private set; }
        public bool CanDownloadModel { get; private set; }

        private bool downloadingAll;

        private Dictionary<string, WebClient> downloads = new Dictionary<string, WebClient>();

        private CancellationTokenSource cancelSource = new CancellationTokenSource();

        public UpdateWindow()
        {
            InitializeComponent();

            DataContext = this;

            ContentRendered += OnStartup;
        }

        //XAML parser would spit out weird errors if this would be called during 
        //window construction, so we'll have to do it after the first rendering
        private void OnStartup(object sender, EventArgs e)
        {
            CheckDependencies();

            //window context for cross-thread UI update
            Downloader.WindowContext = this;

            if (ButtonOK.IsEnabled)
                OnConfirm(null, null);
        }

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            Close();
        }

        private void OnDownloadPython(object sender, RoutedEventArgs e)
        {
            if (CanDownloadPython)
                Task.Factory.StartNew(() =>
                {
                    downloads.Add("pyDL", Downloader.Download(Config.Get.Dependencies["python"],
                        "pythonInstall.exe", ProgressPython));
                }).ContinueWith(result => 
                {
                    MessageBox.Show("Bitte beachten: Bei der Installation das Häkchen bei 'Add Python to PATH' setzen!",
                        "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                    Downloader.Install("pythonInstall.exe", CheckDependencies);
                });
        }

        private void OnDownloadEspeak(object sender, RoutedEventArgs e)
        {
            if (CanDownloadEspeak)
                Task.Factory.StartNew(() =>
                {
                    downloads.Add("speakDL", Downloader.Download(Config.Get.Dependencies["espeak"],
                        "espeakInstall.exe", ProgressEspeak));
                }).ContinueWith(result => Downloader.Install("espeakInstall.exe", CheckDependencies));
        }

        private void OnDownloadDependencies(object sender, RoutedEventArgs e)
        {
            var script = Environment.CurrentDirectory + @"\GameTTS\install.ps1";

            ProcessStartInfo processInfo = new ProcessStartInfo()
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy unrestricted \"{script}\"",
            };

            var task = Task.Factory.StartNew(() =>
            {
                using (Process process = Process.Start(processInfo))
                {
                }
            }).ContinueWith(result => CheckDependencies());
        }

        private void OnDownloadModel(object sender, RoutedEventArgs e)
        {
            if (!CanDownloadEspeak)
                Task.Factory.StartNew(() =>
                {
                    downloads.Add("modelDL", Downloader.Download(Config.Get.Models[Config.Get.CurrentModel],
                        @"GameTTS\vits\model\" + Config.Get.CurrentModel, ProgressModel));
                }).ContinueWith(result => CheckDependencies());
        }

        private void OnDownloadAll(object sender, RoutedEventArgs e)
        {
            downloadingAll = true;
            OnDownloadPython(null, null);
            OnDownloadEspeak(null, null);
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            foreach(var client in downloads.Values)
                client.CancelAsync();
        }

        private void CheckDependencies()
        {
            Dispatcher.Invoke(delegate
            {
                CanDownloadPython = !Dependencies.IsInstalled("python", "Python");
                CanDownloadEspeak = !Dependencies.IsInstalled("espeak", "speak text-to-speech:");
                CanDownloadDependencies = !CanDownloadPython && !CanDownloadEspeak
                    && !Directory.Exists(@"GameTTS\.venv");
                CanDownloadModel = !File.Exists(@"GameTTS\vits\model\" + Config.Get.CurrentModel);

                if (!CanDownloadPython)
                {
                    TBPythonVersion.Text = Dependencies.GetVersion("python", "Python");
                    TBPythonVersion.Foreground = Brushes.Green;
                }
                else
                {
                    TBPythonVersion.Text = "k.A.";
                    TBPythonVersion.Foreground = Brushes.Red;
                }

                if (!CanDownloadEspeak)
                {
                    TBeSpeakVersion.Text = Dependencies.GetVersion("espeak", "speak text-to-speech:");
                    TBeSpeakVersion.Foreground = Brushes.Green;
                }
                else
                {
                    TBeSpeakVersion.Text = "k.A.";
                    TBeSpeakVersion.Foreground = Brushes.Red;
                }

                if (!CanDownloadDependencies)
                {
                    TBDependencies.Text = "installiert";
                    TBDependencies.Foreground = Brushes.Green;
                }
                else
                {
                    TBDependencies.Text = "nicht installiert";
                    TBDependencies.Foreground = Brushes.Red;

                    if (downloadingAll)
                        OnDownloadDependencies(null, null);
                }

                if (!CanDownloadModel)
                {
                    TBModel.Text = Config.Get.CurrentModel;
                    TBModel.Foreground = Brushes.Green;
                }
                else
                {
                    TBModel.Text = "nicht installiert";
                    TBModel.Foreground = Brushes.Red;

                    if (downloadingAll)
                        OnDownloadModel(null, null);
                }

                ButtonOK.IsEnabled = !CanDownloadPython && !CanDownloadEspeak && !CanDownloadDependencies && !CanDownloadModel;
            });
        }

        private double GetDLProgress(DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;

            return int.Parse(Math.Truncate(percentage).ToString());
        }
    }
}
