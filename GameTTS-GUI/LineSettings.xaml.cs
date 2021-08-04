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
using System.Windows.Shapes;

namespace GameTTS_GUI
{
    /// <summary>
    /// Interaction logic for LineSettings.xaml
    /// </summary>
    public partial class LineSettingsWindow : Window
    {
        private LineSettings settings;

        public LineSettingsWindow(LineSettings settings)
        {
            InitializeComponent();
            this.settings = settings;
            DataContext = this.settings;
        }

        private void OnReset(object sender, RoutedEventArgs e)
        {
            settings.Reset();
            SLSpeed.Value = settings.Speed;
            SLVarA.Value = settings.VarianceA;
            SLVarB.Value = settings.VarianceB;
        }

        private void OnApply(object sender, RoutedEventArgs e)
        {
            settings.Speed = SLSpeed.Value;
            settings.VarianceA = SLVarA.Value;
            settings.VarianceB = SLVarB.Value;

            Config.Get.SettingSpeed = settings.Speed;
            Config.Get.SettingVarianceA = settings.VarianceA;
            Config.Get.SettingVarianceB = settings.VarianceB;
            Config.Save();

            Close();
        }
    }
}
