using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GameTTS_GUI
{
    class VoiceLine
    {
        public string Game { get; set; }
        public string Text { get; set; }
        public string Voice { get; set; }
        public string Audio { get; set; }
        public RoutedEventHandler Save { get;  set;}
        public string Path { get; set; }

        public VoiceLine()
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
