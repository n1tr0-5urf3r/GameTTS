using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameTTS_GUI
{
    public class LineSettings
    {
        public double VarianceA { get; set; }
        public double VarianceB { get; set; }
        public double Speed { get; set; }

        public LineSettings()
        {
            Reset();
        }

        public void Reset()
        {
            VarianceA = 0.58;
            VarianceB = 0.8;
            Speed = 1.0;
        }
    }
}
