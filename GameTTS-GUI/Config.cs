using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameTTS_GUI.Updater;
using Newtonsoft.Json;

namespace GameTTS_GUI
{
    public class Config
    {
        private static readonly Config _instance;
        private const int CURRENT_VERSION = 1;

        public int ConfigVersion { get; set; } = CURRENT_VERSION;
        public string OutputPath { get; set; }
        public string CsvPathstring { get; set; }
        public string OutputFormat { get; set; } = "WAV";
        public int ModelVersion { get; set; }
        public string UpdateURL { get; set; }
        public int InstallScriptVersion { get; set; }

        [JsonIgnore]
        public Dictionary<string, int> FileVersions { get; private set; }
        [JsonIgnore]
        public Dictionary<string, Dependency> Dependencies { get; set; }

        //get singelton
        [JsonIgnore]
        public static Config Get { get => _instance; }

        [JsonConstructor]
        private Config() { }

        static Config()
        {
            if (_instance == null)
                if (File.Exists("appConfig.json"))
                    _instance = JsonConvert.DeserializeObject<Config>(File.ReadAllText("appConfig.json"));
                else
                    _instance = new Config();

            _instance.FileVersions = new Dictionary<string, int> 
            { 
                { "pyDependencies", _instance.InstallScriptVersion }, 
                { "model", _instance.ModelVersion } 
            };
        }

        public static void Save() => File.WriteAllText("appConfig.json", JsonConvert.SerializeObject(_instance));
    }
}
