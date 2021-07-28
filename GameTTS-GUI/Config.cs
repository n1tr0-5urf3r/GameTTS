using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        public string CurrentModel { get; set; } = "G_700000.pth";

        [JsonIgnore]
        public Dictionary<string, string> Models { get; private set; }
        [JsonIgnore]
        public Dictionary<string, string> Dependencies { get; private set; }

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

            _instance.Models = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(@"Resources\json\model-urls.json"));
            _instance.Dependencies = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(@"Resources\json\dependency-urls.json"));
        }

        public static void Save() => File.WriteAllText("appConfig.json", JsonConvert.SerializeObject(_instance));
    }
}
