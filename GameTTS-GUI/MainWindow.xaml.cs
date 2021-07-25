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
        private List<VoiceLine> listSource = new List<VoiceLine>();
        Dictionary<string, Dictionary<string, int>> VoiceMapping;
        System.Media.SoundPlayer Player = new System.Media.SoundPlayer();

        string[] games;
        List<string>[] voiceSources;

        LineSettings settings = new LineSettings();

        public MainWindow()
        {
            InitializeComponent();
            this.Closing += (sender, e) =>
            {
                if(Directory.Exists("tmp"))
                {
                    string[] files = Directory.GetFiles("tmp", "*");
                    foreach (var file in files)
                        File.Delete(file);
                    Directory.Delete("tmp");
                }
            };


            //load json voice mapping
            VoiceMapping = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>
                (File.ReadAllText("Resources/game_speaker_map.json"));

            games = VoiceMapping.Keys.ToArray();
            voiceSources = new List<string>[games.Length];
            int i = 0;
            foreach(var key in VoiceMapping.Keys)
            {
                voiceSources[i] = VoiceMapping[key].Keys.ToList();
                ++i;
            }

            CBGame.ItemsSource = games;
            FileList.ItemsSource = listSource;
            CBVoice.ItemsSource = voiceSources[0];

            CBGame.SelectionChanged += (sender, e) =>
            {
                CBVoice.ItemsSource = voiceSources[(sender as ComboBox).SelectedIndex];
                CBVoice.SelectedIndex = 0;
            };

            //restore preferences
            ExportPathBox.Text = Config.Get.OutputPath;
            DefaultCSVBox.Text = Config.Get.CsvPathstring;
        }

        private void OnGenerate(object sender, RoutedEventArgs e)
        {
            var line = new VoiceLine { Game = CBGame.Text, Voice = CBVoice.Text, Text = LineText.Text };
            
            if (!Directory.Exists("tmp"))
                Directory.CreateDirectory("tmp");

            //ps script/python args
            string args = $"'{LineText.Text}' {VoiceMapping[CBGame.Text][CBVoice.Text]} '{CBVoice.Text}' 0.58 0.8 1";
                //py        exec file           text to speak      speaker ID/name  file content?   variation a/b  speed  path    extension
                //"python     .\\main.py   'Es funktioniert tatsächlich'    1 'Ash'           false           0.58 0.8    1   false   wav";
                //"'Hallo, ich bin Ash.' 1 Ash 0.58 0.8 1";

            //call python stuff here
            {
                var script = Environment.CurrentDirectory + @"\GameTTS\run.ps1";

                ProcessStartInfo processInfo = new ProcessStartInfo()
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy unrestricted \"{script}\" " + args,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(processInfo))
                {
                    using (StreamReader myStreamReader = process.StandardOutput)
                    {
                        var outputString = myStreamReader.ReadLine();
                        process.WaitForExit();

                        Console.WriteLine(outputString);
                    }
                }
            }

            string fileName = null;
            if (string.IsNullOrEmpty(FileNameBox.Text))
                fileName = $"{line.Game}_{line.Voice}_{line.GetHashCode()}";
            else
                fileName = FileNameBox.Text;

            string tempFile = "tmp/" + fileName + ".wav";
            line.Path = tempFile;

            listSource.Add(line);
            FileList.Items.Refresh();
        }

        private void OnDeleteItem(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete || e.Key == Key.Back)
                if(FileList.SelectedIndex >= 0)
                {
                    listSource.RemoveAt(FileList.SelectedIndex);
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
            string path;
            System.Windows.Forms.FolderBrowserDialog folder = new System.Windows.Forms.FolderBrowserDialog();
            if (folder.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                path = folder.SelectedPath;
                ExportPathBox.Text = path;
            }
        }

        private void PlayVoiceSample(object sender, RoutedEventArgs e)
        {
            int index = GetVoiceIndex(CBGame.Text, CBVoice.Text);

            string path = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
                        + @"/Resources/VoiceSamples/" + index.ToString() + ".wav";
            Player.SoundLocation = path;
            Player.Play();
        }

        private void OnSettings(object sender, RoutedEventArgs e)
        {
            LineSettingsWindow window = new LineSettingsWindow(settings);
            window.Owner = this;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private void ExportAll(object sender, RoutedEventArgs e)
        {
            if(string.IsNullOrEmpty(ExportPathBox.Text)
            || !Directory.Exists(ExportPathBox.Text))
            {
                MessageBox.Show("Der angegebene Export-Pfad ist ungültig!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string path = ExportPathBox.Text;

            if (!path.EndsWith("\\"))
                path += "\\";
                  
            foreach(var item in listSource)
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
            listSource.Clear();
            FileList.Items.Refresh();
        }

        private int GetVoiceIndex(string game, string voice) => VoiceMapping[game][voice];

        private void ShowFileNameBox(object sender, RoutedEventArgs e) => FileNamePanel.Visibility = Visibility.Visible;

        private void HideFileNameBox(object sender, RoutedEventArgs e) => FileNamePanel.Visibility = Visibility.Collapsed;
    }
}
