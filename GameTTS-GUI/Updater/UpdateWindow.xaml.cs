using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using static GameTTS_GUI.Updater.DependencyManager;

namespace GameTTS_GUI.Updater
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : Window
    {
        /// <summary>
        /// Binding for OK button enabled status
        /// </summary>
        public bool CanContinue { get; set; } = true;

        public UpdateWindow()
        {
            InitializeComponent();

            DataContext = this;

            ContentRendered += OnStartup;
            Closing += CloseRequest;
        }

        /// <summary>
        /// XAML parser would spit out weird errors if this would be called during 
        /// window construction, so we'll have to do start up logic after the first rendering.
        /// </summary>
        private void OnStartup(object sender, EventArgs e)
        {
            //for cross-thread UI update
            DependencyManager.WindowDispatcher = Dispatcher;

            //set connection status change handling
            Connection.OnChecked += (status) =>
            {
                Dispatcher.Invoke(() => UpdateConnectionStatus(status));

                if(status == ConnectionStatus.ConnectionReestablished)
                    CheckDependencies();
            };

            DependencyManager.OnUpdateFileLoaded += () =>
            {
                CheckDependencies();
                if (CanContinue)
                    OnConfirm(null, null);
            };

            //automatically close the window and get to the main app
            DependencyManager.OnUpdateFinished += () =>
            {
                if (CanContinue)
                    OnConfirm(null, null);
            };

            //run a connection check once before we start, then try to
            //retrieve update information
            if (Connection.CheckConnection() == ConnectionStatus.Connected)
                DependencyManager.GetUpdate();

            //check connection status every 5 seconds for changes
            Connection.RunWatcher(5000);
        }

        /// <summary>
        /// Button click action for OK button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();

            Close();
        }

        /// <summary>
        /// Reflect connection status on UI
        /// </summary>
        /// <param name="status"></param>
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

        /// <summary>
        /// Add python dependency to manager's queue (see <see cref="DependencyManager.QueueInstall(string, InstallTask)"/>).
        /// </summary>
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

        /// <summary>
        /// Add install script execution to manager's queue (see <see cref="DependencyManager.QueueInstall(string, InstallTask)"/>).
        /// </summary>
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

        /// <summary>
        /// Start model download. Doesn't require installation or other special treatment.
        /// </summary>
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

        /// <summary>
        /// Cancel button delegate.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCancel(object sender, RoutedEventArgs e) => Close();

        /// <summary>
        /// Fired when window is about to be closed (either by program or user input).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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

        /// <summary>
        /// Check all method that gets the install status of dependencies and automatically queues the ones
        /// not installed. Might get called quite a lot, so there's room for improvement.<br/>
        /// <see cref="DependencyManager"/> handles queuing, so no duplicates are being created by calling
        /// this method multiple times.
        /// </summary>
        private void CheckDependencies()
        {
            //only call check function once, result is needed multiple times
            //esp. model checking takes time regarding checksum calculation
            bool hasPy = DependencyManager.IsPythonInstalled;
            bool hasPyDep = DependencyManager.IsPyDepInstalled;
            bool hasMdl = DependencyManager.IsModelInstalled;

            Dispatcher.Invoke(delegate
            {
                if (hasPy)
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
                }

                if (hasPyDep)
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
                }

                if (hasMdl)
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
                }
            });

            CanContinue = hasPy && hasPyDep && hasMdl;

            if(!CanContinue)
                DependencyManager.InstallAll();
        }
    }
}
