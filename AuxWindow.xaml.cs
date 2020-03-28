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

using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Utils;

namespace AudioWave
{
    /// <summary>
    /// Interaction logic for AuxWindow.xaml
    /// </summary>
    public partial class AuxWindow : Window
    {
        
        public AuxWindow()
        {
            InitializeComponent();
            InitLists();
        }
        private void InitLists()
        {
            foreach (var a in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                var l = new ListBoxItem();
                l.Content = a.FriendlyName;
                list_input.Items.Add(l);
            }
            foreach (var a in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var l = new ListBoxItem();
                l.Content = a.FriendlyName;
                list_output.Items.Add(l);
            }
        }
        private void On_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled)
                return;
            var label = (Label)e.OriginalSource;
            if (active(label))
            {
                label.Foreground = Brushes.Green;
                ActivateModes(true, label);
            }
            else
            {
                label.Foreground = Brushes.Black;
                ActivateModes(false, label);
            }
        }
        private void ActivateModes(bool activate, Label label)
        {
            if (label == label_mic)
            {
                if (activate)
                {
                    SideWindow.Instance.On_Stop(null, null);
                }
                else
                {

                }
            }
            else if (label == label_monitor)
            {
                if (activate)
                {
                    SideWindow.Instance.On_Stop(null, null);
                }
                else
                {

                }
            }
        }
        private bool active(Label label)
        {
            return label.Foreground != Brushes.Black;
        }

        private void On_Refresh(object sender, MouseButtonEventArgs e)
        {
            InitLists();
        }

        private void input_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (e.Handled)
                return;
            foreach (var a in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                var list = (ListBox)e.OriginalSource;
                if (list.Items[list.SelectedIndex].ToString().Contains(a.FriendlyName))
                {
                    MainWindow.Instance.wave.defaultInput = a;
                    return;
                }
            }
        }

        private void output_Change(object sender, SelectionChangedEventArgs e)
        {
            if (e.Handled)
                return;
            foreach (var a in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var list = (ListBox)e.OriginalSource;
                if (list.Items[list.SelectedIndex].ToString().Contains(a.FriendlyName))
                {
                    MainWindow.Instance.wave.defaultOutput = a;
                    return;
                }
            }
        }
    }
}
