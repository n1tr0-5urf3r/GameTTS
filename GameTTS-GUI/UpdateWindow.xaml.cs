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
using static GameTTS_GUI.Dependencies;

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

        private bool downloadingModel;
        private bool modelDownloaded;

        private Dictionary<string, WebClient> downloads = new Dictionary<string, WebClient>();
        private List<InstallTask> installs = new List<InstallTask>();

        public UpdateWindow()
        {
            InitializeComponent();

            DataContext = this;

            ContentRendered += OnStartup;
            Closing += CloseRequest;
        }

        //XAML parser would spit out weird errors if this would be called during 
        //window construction, so we'll have to do it after the first rendering
        private void OnStartup(object sender, EventArgs e)
        {
            CheckDependencies();

            if (ButtonOK.IsEnabled)
                OnConfirm(null, null);

            //window context for cross-thread UI update
            Dependencies.WindowContext = this;

            //download and install all necessary one after the other
            StartUpdate();

            if (ButtonOK.IsEnabled)
                OnConfirm(null, null);
        }

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            Close();
        }

        private void StartUpdate()
        {
            installs.Clear();
            DownloadModel();
            QueuePython();
            QueueEspeak();
            QueueDependencies();
            Dependencies.InstallAll(installs);
        }

        private void QueuePython()
        {
            if (CanDownloadPython)
            {
                installs.Add(new InstallTask
                {
                    URL = Config.Get.Dependencies["python"],
                    FilePath = "pythonInstall.exe",
                    ProgressBar = ProgressPython,
                    LoadingLabel = TBPythonVersion,
                    PreInstall = () =>
                        MessageBox.Show("Bitte beachten: Bei der Installation das Häkchen bei 'Add Python to PATH' setzen!",
                            "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information),
                    PostInstall = () => VerifyInstallation("python", "Python", 3)
                });
            }
        }

        private void QueueEspeak()
        {
            if (CanDownloadEspeak)
            {
                installs.Add(new InstallTask
                {
                    URL = Config.Get.Dependencies["espeak"],
                    FilePath = "espeakInstall.exe",
                    ProgressBar = ProgressEspeak,
                    LoadingLabel = TBeSpeakVersion,
                    PreInstall = () =>
                        MessageBox.Show("Bitte beachten: Bei der Installation zusätzlich in ein leeres Feld 'de-de' eintragen.",
                            "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information),
                    PostInstall = () => VerifyInstallation("espeak", "speak text-to-speech:")
                });
            }
        }

        private void QueueDependencies()
        {
            if(CanDownloadDependencies)
            {
                installs.Add(new InstallTask
                {
                    //URL = Config.Get.Dependencies["espeak"], //need url later
                    FilePath = Environment.CurrentDirectory + @"\GameTTS\install.ps1",
                    ProgressBar = ProgressPyDependencies,
                    LoadingLabel = TBDependencies,
                    PreInstall = () =>
                        MessageBox.Show("Bitte beachten: Bei der Installation zusätzlich in ein leeres Feld 'de-de' eintragen.",
                            "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information),
                });
            }
        }

        private void DownloadModel()
        {
            if (CanDownloadModel)
            {
                downloadingModel = true;
                GDriveDownloader gdl = null;
                Dependencies.DownloadGDrive(ref gdl, Config.Get.Models[Config.Get.CurrentModel],
                    @"GameTTS\vits\model\" + Config.Get.CurrentModel, ProgressModel);
                downloads.Add("modelDL", gdl.Client);
                gdl.DownloadFileCompleted += (s, a) =>
                {
                    modelDownloaded = true;
                    CheckDependencies();
                };
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Close();

        private void CloseRequest(object sender, CancelEventArgs e)
        {
            var result = MessageBox.Show("Setup/Update wirklich abbrechen?",
                                "Hinweis", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            if (downloads.Count > 0)
            {
                foreach (var client in downloads.Values)
                    client.CancelAsync();
            }

            //remove unfinshed downloads
            try
            {
                if (downloadingModel && !modelDownloaded)
                    if (File.Exists(@"GameTTS\vits\model\" + Config.Get.CurrentModel))
                        File.Delete(@"GameTTS\vits\model\" + Config.Get.CurrentModel);

                if (File.Exists("espeakInstall.exe"))
                    File.Delete("espeakInstall.exe");

                if (File.Exists("pythonInstall.exe"))
                    File.Delete("pythonInstall.exe");
            }
            catch { }
        }

        private void CheckDependencies()
        {
            Dispatcher.Invoke(delegate
            {
                CanDownloadPython = !Dependencies.IsInstalled("python", "Python", 3);
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
                    TBPythonVersion.Text = "nicht installiert";
                    TBPythonVersion.Foreground = Brushes.Red;
                }

                if (!CanDownloadEspeak)
                {
                    TBeSpeakVersion.Text = Dependencies.GetVersion("espeak", "speak text-to-speech:");
                    TBeSpeakVersion.Foreground = Brushes.Green;
                }
                else
                {
                    TBeSpeakVersion.Text = "nicht installiert";
                    TBeSpeakVersion.Foreground = Brushes.Red;
                }

                if (Directory.Exists(@"GameTTS\.venv"))
                {
                    TBDependencies.Text = "installiert";
                    TBDependencies.Foreground = Brushes.Green;
                }
                else
                {
                    TBDependencies.Text = "nicht installiert";
                    TBDependencies.Foreground = Brushes.Red;
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
                }

                ButtonOK.IsEnabled = !CanDownloadPython && !CanDownloadEspeak && !CanDownloadDependencies && !CanDownloadModel;
            });
        }

        private bool VerifyInstallation(string execName, string versionPrefix, int? major = null, int? minor = null)
        {
            if (!Dependencies.IsInstalled(execName, versionPrefix, major, minor))
            {
                var result = MessageBox.Show("Installation fehlgeschlagen oder abgebrochen. Erneut versuchen?",
                    "Fehler", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    Dispatcher.Invoke(delegate
                    {
                        ProgressPython.Value = 0;
                        StartUpdate();
                    });
                    return false;
                }
                else if (result == MessageBoxResult.No)
                {
                    Dispatcher.Invoke(delegate
                    {
                        Close();
                    });
                    return false;
                }
            }

            Dispatcher.Invoke(delegate
            {
                CheckDependencies();
            });

            return true;
        }
    }
}
