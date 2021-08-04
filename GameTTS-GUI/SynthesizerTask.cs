using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GameTTS_GUI
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TaskType
    {
        [JsonProperty("synth_text")]
        SynthText,
        [JsonProperty("synth_csv")]
        SynthCSV,
        [JsonProperty("synth_setting")]
        SynthSetting,
        [JsonProperty("exit")]
        SynthExit
    }

    class SynthesizerTask
    {
        public TaskType Task { get; set; }
        public string Game { get; set; }
        public string Text { get; set; }
        public string Voice { get; set; }
        public string Path { get; set; }
        public double VarianceA { get; set; }
        public double VarianceB { get; set; }
        public double Speed { get; set; }
        [JsonIgnore]
        public string Audio { get; set; }
        [JsonIgnore]
        public RoutedEventHandler Save { get;  set;}

        public SynthesizerTask()
        {
            Audio = "0:00 / 0:02 ---";

            Save = (sender, e) => 
            {
                //save single generate voice lines here
                Console.WriteLine("huh okay");
            };
        }
    }
}
