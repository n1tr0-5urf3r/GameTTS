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
using static GameTTS_GUI.Updater.Dependencies;

namespace GameTTS_GUI.Updater
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : Window
    {
        #region -- UI data bindings

        //progress bar python
        public double PyProgress;
        public SolidColorBrush PyProgressColor;

        //progress bar model download
        public double ModelProgress;
        public SolidColorBrush ModelProgressColor;

        //progress bar ps script installer

        #endregion
        public bool CanDownloadPython { get; private set; }
        public bool CanDownloadDependencies { get; private set; }
        public bool CanDownloadModel { get; private set; }

        private bool updating;
        private bool modelDownloaded;
        private bool canProceed;

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
            //window context for cross-thread UI update
            Dependencies.WindowContext = this;

            Dependencies.OnConnectionCheck += (hasConnection) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (hasConnection)
                    {
                        TBStatus.Text = "[.] verbunden";
                        TBStatus.Foreground = Brushes.Green;
                    }
                    else
                    {
                        TBStatus.Text = "[ ] nicht verbunden";
                        TBStatus.Foreground = Brushes.Red;
                        return;
                    }
                });
            };

            Dependencies.OnUpdateFileLoaded += CheckDependencies;
            Dependencies.OnUpdateFinished += () =>
            {
                if (ButtonOK.IsEnabled)
                    OnConfirm(null, null);
            };

            if (Dependencies.CheckConnection())
                Dependencies.GetUpdate();

            Dependencies.RunConnectionWatcher(5000);
        }

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            canProceed = true;
            Close();
        }

        private void StartUpdate()
        {
            if (updating)
                return;

            updating = true;
            installs.Clear();
            DownloadModel();
            QueuePython();
            QueueDependencies();
            Dependencies.OnUpdateFinished += () =>
            {
                updating = false;
                CheckDependencies();
            };

            Dependencies.InstallAll(installs);
        }

        private void QueuePython()
        {
            if (CanDownloadPython)
            {
                installs.Add(new InstallTask
                {
                    URL = Config.Get.Dependencies["python"].URL,
                    FilePath = @"GameTTS\tmp\pythonInstall.exe",
                    ProgressBar = ProgressPython,
                    LoadingLabel = TBPythonVersion,
                    PreInstall = () =>
                        MessageBox.Show("Bitte beachten: Bei der Installation das Häkchen bei 'Add Python to PATH' setzen!",
                            "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information),
                    PostInstall = () => VerifyInstallation("python")
                });
            }
        }

        private void QueueDependencies()
        {
            if(CanDownloadDependencies)
            {
                installs.Add(new InstallTask
                {
                    //URL = Config.Get.Dependencies["pyDependencies"], //need url later
                    FilePath = Environment.CurrentDirectory + @"\GameTTS\install.ps1",
                    ProgressBar = ProgressPyDependencies,
                    LoadingLabel = TBDependencies,
                    PostInstall = () => 
                    { 
                        CheckDependencies();
                        Config.Get.InstallScriptVersion = Config.Get.Dependencies["pyDependencies"].Version.Major;
                        Config.Save();
                        return true; }
                });
            }
        }

        private void DownloadModel()
        {
            if (CanDownloadModel)
            {
                string modelPath = @"GameTTS\tmp\" + Config.Get.Dependencies["model"].Name;
                GDriveDownloader gdl = null;
                TBModel.Text = "lade...";
                TBModel.Foreground = Brushes.Black;
                Dependencies.DownloadGDrive(ref gdl, Config.Get.Dependencies["model"].URL,
                    modelPath, ProgressModel);
                downloads.Add("modelDL", gdl.Client);
                gdl.DownloadFileCompleted += (s, a) =>
                {
                    if (File.Exists(modelPath))
                    {
                        File.Move(modelPath, @"GameTTS\vits\model\" + Config.Get.Dependencies["model"].Name);
                        modelDownloaded = true;
                        ProgressModel.Foreground = Brushes.Green;
                        Config.Get.ModelVersion = Config.Get.Dependencies["model"].Version.Major;
                        Config.Save();
                        CheckDependencies();
                    }
                };
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Close();

        private void CloseRequest(object sender, CancelEventArgs e)
        {
            if (!canProceed)
            {
                var result = MessageBox.Show("Setup/Update wirklich abbrechen?",
                                    "Hinweis", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            
                if (Dependencies.CurrentDownload != null)
                    Dependencies.CurrentDownload.CancelAsync();
                if (Dependencies.CurrentGDriveDownload != null)
                    Dependencies.CurrentGDriveDownload.Client.CancelAsync();
            }
        }

        private void CheckDependencies()
        {
            bool needsUpdate = false;
            var py = Config.Get.Dependencies["python"];
            var pyDeps = Config.Get.Dependencies["pyDependencies"];
            var model = Config.Get.Dependencies["model"];

            CanDownloadPython = !Dependencies.IsInstalled(py);
            CanDownloadDependencies = !Directory.Exists(@"GameTTS\.venv") 
                && !Dependencies.IsInstalled("pyDependencies");
            CanDownloadModel = !File.Exists(@"GameTTS\vits\model\" + Config.Get.Dependencies["model"].Name)
                && !Dependencies.IsInstalled("model");

            Dispatcher.Invoke(delegate
            {
                if (!CanDownloadPython)
                {
                    TBPythonVersion.Text = Dependencies.GetVersion("python").ToString();
                    TBPythonVersion.Foreground = Brushes.Green;
                }
                else
                {
                    TBPythonVersion.Text = "nicht installiert";
                    TBPythonVersion.Foreground = Brushes.Red;
                    needsUpdate = true;
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
                    needsUpdate = true;
                }

                if (!CanDownloadModel)
                {
                    TBModel.Text = Config.Get.Dependencies["model"].Name;
                    TBModel.Foreground = Brushes.Green;
                }
                else
                {
                    TBModel.Text = "nicht installiert";
                    TBModel.Foreground = Brushes.Red;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    StartUpdate();
                    return;
                }

                ButtonOK.IsEnabled = !CanDownloadPython && !CanDownloadDependencies && !CanDownloadModel;
            });
        }

        private bool VerifyInstallation(string key)
        {
            if (!Dependencies.IsInstalled(Config.Get.Dependencies[key]))
            {
                var result = MessageBox.Show("Installation fehlgeschlagen oder abgebrochen. Erneut versuchen?",
                    "Fehler", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    Dispatcher.Invoke(delegate
                    {
                        ProgressPython.Value = 0;
                        updating = false;
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
