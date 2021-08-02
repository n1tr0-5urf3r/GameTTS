using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GameTTS_GUI.Updater
{
    public class Dependency
    {
        public string Name { get; set; }
        [JsonProperty]
        private int versionMajor { get; set; }
        [JsonProperty]
        private int versionMinor { get; set; }
        public string URL { get; set; }
        [JsonIgnore]
        public Version Version { get => new Version(versionMajor, versionMinor); }
        public string Checksum { get; set; }
    }
}
