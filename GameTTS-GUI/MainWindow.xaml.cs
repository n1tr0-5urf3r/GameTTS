using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using System.Diagnostics;

namespace GameTTS_GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainData data;
        private Synthesizer synth = new Synthesizer();
        LineSettingsWindow settingsDialog;

        public MainWindow()
        {
            InitializeComponent();

            data = new MainData();

            CBGame.ItemsSource = data.GameList;
            FileList.ItemsSource = data.OutputListData;
            CBVoice.ItemsSource = data.VoiceLists[0];

            CBGame.SelectionChanged += (sender, e) =>
            {
                CBVoice.ItemsSource = data.VoiceLists[(sender as ComboBox).SelectedIndex];
                CBVoice.SelectedIndex = 0;
            };

            //restore preferences
            ExportPathBox.Text = Config.Get.OutputPath;
            DefaultCSVBox.Text = Config.Get.CsvPathstring;
            CBAudioFormat.SelectedIndex = (int)Config.Get.OutputFormat;

            //send first settings to synth process
            synth.StartProcess();
            SendSynthSettings();
        }

        private void OnGenerate(object sender, RoutedEventArgs e)
        {
            int voiceIndex = GetVoiceIndex(CBGame.Text, CBVoice.Text);
            var line = new SynthesizerTask { Task = TaskType.SynthText, Game = CBGame.Text, Voice = CBVoice.Text, VoiceID = voiceIndex, Text = LineText.Text };

            if (!Directory.Exists(Config.TempPath))
                Directory.CreateDirectory(Config.TempPath);

            //ps script/python args
            //string args = $"'{LineText.Text}' {data.VoiceMapping[CBGame.Text][CBVoice.Text]} '{CBVoice.Text}' 0.58 0.8 1";
            //py        exec file           text to speak      speaker ID/name  file content?   variation a/b  speed  path    extension
            //"python     .\\main.py   'Es funktioniert tatsächlich'    1 'Ash'           false           0.58 0.8    1   false   wav";
            //"'Hallo, ich bin Ash.' 1 Ash 0.58 0.8 1";

            //call python stuff here
            //synth.SendInput("lol");
            synth.SendInput(line);

            string fileName = null;
            if (string.IsNullOrEmpty(FileNameBox.Text))
                fileName = $"{line.Game}_{line.Voice}_{line.GetHashCode()}";
            else
                fileName = FileNameBox.Text;

            string tempFile = "tmp/" + fileName + ".wav";
            line.Path = tempFile;

            data.OutputListData.Add(line);
            FileList.Items.Refresh();
        }

        private void OnDeleteItem(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
                if (FileList.SelectedIndex >= 0)
                {
                    data.OutputListData.RemoveAt(FileList.SelectedIndex);
                    FileList.Items.Refresh();
                }
        }

        private void SelectCSV(object sender, RoutedEventArgs e)
        {
            string path;
            OpenFileDialog file = new OpenFileDialog();
            if (file.ShowDialog().Value)
            {
                path = file.FileName;
                //import CVS here
            }
        }

        private void SelectExportPath(object sender, RoutedEventArgs e)
        {
            string path = "";
            System.Windows.Forms.FolderBrowserDialog folder = new System.Windows.Forms.FolderBrowserDialog();
            if (folder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                path = folder.SelectedPath;
                ExportPathBox.Text = path;
            }

            Config.Get.OutputPath = path;
            Config.Save();
        }

        private void PlayVoiceSample(object sender, RoutedEventArgs e)
        {
            int index = GetVoiceIndex(CBGame.Text, CBVoice.Text);

            string path = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
                        + @"/Resources/VoiceSamples/" + index.ToString() + ".wav";
            data.Player.SoundLocation = path;
            data.Player.Play();
        }

        private void OnSettings(object sender, RoutedEventArgs e)
        {
            if (settingsDialog == null)
            {
                settingsDialog = new LineSettingsWindow(data.Settings);
                settingsDialog.Owner = this;
                settingsDialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                settingsDialog.Apply += () =>
                {
                    Config.Get.SettingSpeed = data.Settings.Speed;
                    Config.Get.SettingVarianceA = data.Settings.VarianceA;
                    Config.Get.SettingVarianceB = data.Settings.VarianceB;
                    Config.Save();
                    SendSynthSettings();
                };
            }

            settingsDialog.ShowDialog();
        }

        private void SendSynthSettings()
        {
            synth.SendInput(new SynthesizerTask
            {
                Task = TaskType.SynthSetting,
                VarianceA = data.Settings.VarianceA,
                VarianceB = data.Settings.VarianceB,
                Speed = data.Settings.Speed
            });
        }

        private void ExportAll(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ExportPathBox.Text)
            || !Directory.Exists(ExportPathBox.Text))
            {
                MessageBox.Show("Der angegebene Export-Pfad ist ungültig!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string path = ExportPathBox.Text;

            if (!path.EndsWith("\\"))
                path += "\\";

            foreach (var item in data.OutputListData)
            {
                int index = GetVoiceIndex(item.Game, item.Voice);
                FileInfo info = new FileInfo(item.Path);


                //the actual temp file to copy, uncomment if available
                //File.Copy(item.Path, path + info.Name);

                File.Copy(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
                        + @"/Resources/VoiceSamples/" + index.ToString() + ".wav", path + info.Name);
            }
        }

        private void ClearList(object sender, RoutedEventArgs e)
        {
            data.OutputListData.Clear();
            FileList.Items.Refresh();
        }

        private int GetVoiceIndex(string game, string voice) => data.VoiceMapping[game][voice];

        private void ShowFileNameBox(object sender, RoutedEventArgs e) => FileNamePanel.Visibility = Visibility.Visible;

        private void HideFileNameBox(object sender, RoutedEventArgs e) => FileNamePanel.Visibility = Visibility.Collapsed;

        private void AudioFormatChanged(object sender, SelectionChangedEventArgs e)
        {
            Config.Get.OutputFormat = (AudioFormat)CBAudioFormat.SelectedIndex;
            Config.Save();
        }
    }
}
