using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
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
using static GameTTS_GUI.Updater.DependencyManager;

namespace GameTTS_GUI.Updater
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : Window
    {
        #region -- UI data bindings

        public bool CanContinue { get; set; } = true;

        #endregion

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
            DependencyManager.WindowContext = this;

            Connection.OnChecked += (status) =>
            {
                Dispatcher.Invoke(() => UpdateConnectionStatus(status));

                if(status == ConnectionStatus.ConnectionReestablished)
                    CheckDependencies();
            };

            DependencyManager.OnUpdateFileLoaded += () =>
            {
                CheckDependencies();
            };

            DependencyManager.OnUpdateFinished += () =>
            {
                if (CanContinue)
                    OnConfirm(null, null);
            };

            if (Connection.CheckConnection() == ConnectionStatus.Connected)
                DependencyManager.GetUpdate();

            Connection.RunWatcher(5000);
        }

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            Close();
        }

        public void UpdateConnectionStatus(ConnectionStatus status)
        {
            switch (status)
            {
                case ConnectionStatus.Connected:
                case ConnectionStatus.ConnectionReestablished:
                    TBStatus.Text = "[.] verbunden";
                    TBStatus.Foreground = Brushes.Green;
                    break;
                case ConnectionStatus.Disconnected:
                case ConnectionStatus.ConnectionLost:
                    TBStatus.Text = "[ ] nicht verbunden";
                    TBStatus.Foreground = Brushes.Red;
                    break;
            }
        }

        private void QueuePython()
        {
            if (!DependencyManager.IsPythonInstalled)
            {
                DependencyManager.QueueInstall("python", new InstallTask
                {
                    URL = Config.Get.Dependencies["python"].URL,
                    FilePath = Config.TempPath + "pythonInstall.exe",
                    ProgressBar = ProgressPython,
                    ProgressText = TBPythonVersion,
                    PreInstall = () =>
                        MessageBox.Show("Bitte beachten: Bei der Installation das Häkchen bei 'Add Python to PATH' setzen!",
                            "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information),
                    PostInstall = () =>
                    {
                        if (!IsPythonInstalled)
                        {
                            var result = MessageBox.Show("Installation fehlgeschlagen oder abgebrochen. Erneut versuchen?",
                                "Fehler", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                            if (result == MessageBoxResult.Yes)
                            {
                                Dispatcher.Invoke(delegate
                                {
                                    ProgressPython.Value = 0;
                                    CheckDependencies();
                                });
                                return false;
                            }
                            else
                            {
                                Dispatcher.Invoke(delegate
                                {
                                    Close();
                                });
                                return false;
                            }
                        }

                        CheckDependencies();
                        return true;
                    }
                });
            }
        }

        private void QueueDependencies()
        {
            if(!DependencyManager.IsPyDepInstalled)
            {
                DependencyManager.QueueInstall("pyDependencies", new InstallTask
                {
                    //URL = Config.Get.Dependencies["pyDependencies"], //need url later
                    FilePath = @"GameTTS/install.ps1",
                    TargetDirectory = Config.VirtualEnvPath,
                    ProgressBar = ProgressPyDependencies,
                    ProgressText = TBDependencies,
                    PostInstall = () =>
                    {
                        Config.Get.InstallScriptVersion = Config.Get.Dependencies["pyDependencies"].Version.Major;
                        Config.Save();
                        CheckDependencies();
                        return true;
                    }
                });
            }
        }

        private void DownloadModel()
        {
            if (!DependencyManager.IsModelInstalled)
            {
                string tempModelPath = Config.TempPath + Config.Get.Dependencies["model"].Name;

                TBModel.Text = "lade...";
                TBModel.Foreground = Brushes.Black;

                DependencyManager.DownloadGDrive(new InstallTask
                {
                    FilePath = tempModelPath,
                    URL = Config.Get.Dependencies["model"].URL,
                    ProgressBar = ProgressModel,
                    ProgressText = TBModel,
                    PostInstall = () =>
                    {
                        if (File.Exists(tempModelPath))
                        {
                            try
                            {
                                if (DependencyManager.CheckIntegrity(tempModelPath, Config.Get.Dependencies["model"].Checksum))
                                {
                                    string dest = Config.ModelPath + Config.Get.Dependencies["model"].Name;
                                    if (File.Exists(dest))
                                        File.Delete(dest);

                                    File.Move(tempModelPath, dest);
                                    Dispatcher.Invoke(() => { ProgressModel.Foreground = Brushes.Green; });
                                    Config.Get.ModelVersion = Config.Get.Dependencies["model"].Version.Major;
                                    Config.Save();
                                    CheckDependencies();
                                }
                            }
                            catch (Exception) { }
                        }

                        return false;
                    }
                });
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e) => Close();

        private void CloseRequest(object sender, CancelEventArgs e)
        {
            if (!CanContinue)
            {
                var result = MessageBox.Show("Setup/Update wirklich abbrechen?",
                                    "Hinweis", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                DependencyManager.CancelDownloads();
            }
        }

        private void CheckDependencies()
        {
            Dispatcher.Invoke(delegate
            {
                if (DependencyManager.IsPythonInstalled)
                {
                    TBPythonVersion.Text = DependencyManager.GetVersion("python").ToString();
                    TBPythonVersion.Foreground = Brushes.Green;
                    ProgressPython.Foreground = Brushes.Green;
                    ProgressPython.Value = 100;
                }
                else
                {
                    TBPythonVersion.Text = "nicht installiert";
                    TBPythonVersion.Foreground = Brushes.Red;
                    QueuePython();
                    CanContinue = false;
                }

                if (DependencyManager.IsPyDepInstalled)
                {
                    TBDependencies.Text = "installiert";
                    TBDependencies.Foreground = Brushes.Green;
                    ProgressPyDependencies.Foreground = Brushes.Green;
                    ProgressPyDependencies.Value = 100;
                }
                else
                {
                    TBDependencies.Text = "nicht installiert";
                    TBDependencies.Foreground = Brushes.Red;
                    QueueDependencies();
                    CanContinue = false;
                }

                if (DependencyManager.IsModelInstalled)
                {
                    TBModel.Text = Config.Get.Dependencies["model"].Name;
                    TBModel.Foreground = Brushes.Green;
                    ProgressModel.Foreground = Brushes.Green;
                    ProgressModel.Value = 100;
                }
                else
                {
                    TBModel.Text = "nicht installiert";
                    TBModel.Foreground = Brushes.Red;
                    DownloadModel();
                    CanContinue = false;
                }
            });

            CanContinue = DependencyManager.IsPythonInstalled && DependencyManager.IsPyDepInstalled && DependencyManager.IsModelInstalled;

            if(!CanContinue)
                DependencyManager.InstallAll();
        }
    }
}
