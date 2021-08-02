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
        #region -- internals

        private static readonly Config _instance;
        private const int LATEST_VERSION = 1;

        #endregion

        #region -- serializable properties

        public int ConfigVersion { get; set; } = LATEST_VERSION;
        public string OutputPath { get; set; }
        public string CsvPathstring { get; set; }
        public string OutputFormat { get; set; } = "WAV";
        public int ModelVersion { get; set; }
        public string UpdateURL { get; set; }
        public int InstallScriptVersion { get; set; }

        #endregion

        #region -- non-serializable properties and constants

        [JsonIgnore]
        public const string SpeakerMapPath = @"GameTTS/resources/json-mapping/game_speaker_map.json";
        public const string TempPath = @"GameTTS/tmp/";
        public const string SamplesPath = @"Resources/VoiceSamples/";
        public const string UpdateFilePath = @"Resources/json/update.json";
        public const string ModelPath = @"GameTTS/vits/model/";
        public const string VirtualEnvPath = @"GameTTS/.venv";

        [JsonIgnore]
        public Dictionary<string, int> FileVersions
        {
            get => new Dictionary<string, int> 
                { 
                    { "pyDependencies", _instance.InstallScriptVersion }, 
                    { "model", _instance.ModelVersion } 
                };
        }
        [JsonIgnore]
        public Dictionary<string, Dependency> Dependencies { get; set; }

        #endregion

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
        }

        public static void Save() => File.WriteAllText("appConfig.json", JsonConvert.SerializeObject(_instance, Formatting.Indented));
    }
}
