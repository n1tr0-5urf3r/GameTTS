using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace GameTTS_GUI
{
    internal class MainData
    {
        #region -- static data

        public Dictionary<string, Dictionary<string, int>> VoiceMapping { get; private set; }
        public string[] GameList { get; private set; }
        public List<string>[] VoiceLists { get; private set; }
        public SoundPlayer Player { get; private set; } = new SoundPlayer();

        #endregion

        public List<VoiceLine> OutputListData { get; private set; } = new List<VoiceLine>();
        public LineSettings Settings { get; private set; } = new LineSettings();

        internal MainData()
        {
            //load json voice mapping
            VoiceMapping = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>
                (File.ReadAllText(Config.SpeakerMapPath));

            GameList = VoiceMapping.Keys.ToArray();
            VoiceLists = new List<string>[GameList.Length];
            int i = 0;
            foreach (var key in VoiceMapping.Keys)
            {
                VoiceLists[i] = VoiceMapping[key].Keys.ToList();
                ++i;
            }
        }
    }
}
