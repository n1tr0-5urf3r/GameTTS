using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameTTS_GUI.Updater;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GameTTS_GUI
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AudioFormat
    {
        WAV,
        OGG
    }

    public class Config
    {
        #region -- internals

        private static readonly Config _instance;
        private const int LATEST_VERSION = 1;

        #endregion

        #region -- serializable properties

        /// <summary>
        /// Used to detect any changes if this class gets modified in further updates.
        /// </summary>
        public int ConfigVersion { get; set; } = LATEST_VERSION;
        /// <summary>
        /// The path for any audio files the synthesizer generates.
        /// </summary>
        public string OutputPath { get; set; }
        /// <summary>
        /// Default path to read CSV files from.
        /// </summary>
        public string CsvPathstring { get; set; }
        /// <summary>
        /// Audio format of synthesizer output. Can currently be WAV or OGG.
        /// </summary>
        public AudioFormat OutputFormat { get; set; }
        /// <summary>
        /// Synthesizer setting for variance a.
        /// </summary>
        public double SettingVarianceA { get; set; } = 0.58;
        /// <summary>
        /// Synthesizer setting for variance b.
        /// </summary>
        public double SettingVarianceB { get; set; } = 0.8;
        /// <summary>
        /// Synthesizer setting for speech speed.
        /// </summary>
        public double SettingSpeed { get; set; } = 1.0;

        /// <summary>
        /// Version of the installed model as a single integer (no need for full versioning).
        /// </summary>
        public int ModelVersion { get; set; }
        /// <summary>
        /// URL to the update.json
        /// </summary>
        public string UpdateURL { get; set; }
        /// <summary>
        /// Version of the current install script (no need for full versioning).
        /// </summary>
        public int InstallScriptVersion { get; set; }

        #endregion

        #region -- non-serializable properties and constants

        [JsonIgnore]
        public const string SpeakerMapPath = @"Resources/json/game_speaker_map.json";
        public const string TempPath = @"GameTTS/tmp/";
        public const string SamplesPath = @"Resources/VoiceSamples/";
        public const string UpdateFilePath = @"Resources/json/update.json";
        public const string ModelPath = @"GameTTS/vits/model/";
        public const string VirtualEnvPath = @"GameTTS/.venv";

        /// <summary>
        /// Used to provide a means to get version by dependency key."
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, int> FileVersions
        {
            get => new Dictionary<string, int> 
                { 
                    { "pyDependencies", _instance.InstallScriptVersion }, 
                    { "model", _instance.ModelVersion } 
                };
        }

        /// <summary>
        /// Dictionary of dependencies downloaded and read from update.json
        /// </summary>
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
