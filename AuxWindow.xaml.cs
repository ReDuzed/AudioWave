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
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Utils;

namespace AudioWave
{
    /// <summary>
    /// Interaction logic for AuxWindow.xaml
    /// </summary>
    public partial class AuxWindow : Window
    {
        public static AuxWindow Instance;
        public AuxWindow()
        {
            InitializeComponent();
            Owner = MainWindow.Instance;
            InitLists();
            label_mic.Foreground = Brushes.Black;
            label_monitor.Foreground = Brushes.Black;
            Instance = this;
        }
        private void InitLists()
        {
            list_input.Items.Clear();
            foreach (var a in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                var l = new ListBoxItem();
                l.Content = a.FriendlyName;
                list_input.Items.Add(l);
            }
            list_output.Items.Clear();
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
            var label = (Label)e.Source;
            if (active(label))
            {
                label.Foreground = Brushes.Black;
                ActivateModes(false, label);
            }
            else
            {
                label.Foreground = Brushes.Green;
                ActivateModes(true, label);
            }
        }
        private void ActivateModes(bool activate, Label label)
        {
            if (label == label_mic)
            {
                if (activate)
                {
                    SideWindow.Instance.On_Stop(null, null);
                    MainWindow.Instance.wave.InitCapture(MainWindow.Instance.wave.defaultInput);
                }
                else
                {
                    if (MainWindow.Instance.wave.capture != null)
                        MainWindow.Instance.wave.capture.StopRecording();
                }
            }
            else if (label == label_monitor)
            {
                if (activate)
                {
                    SideWindow.Instance.On_Stop(null, null);
                    MainWindow.Instance.wave.playback = true;
                    MainWindow.Instance.wave.monitor = true;
                }
                else
                {
                    MainWindow.Instance.wave.audioOut.Stop();
                    MainWindow.Instance.wave.monitor = false;
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
                var list = (ListBox)e.Source;
                if (list.SelectedIndex == -1)
                    continue;
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
                var list = (ListBox)e.Source;
                if (list.SelectedIndex == -1)
                    continue;
                if (list.Items[list.SelectedIndex].ToString().Contains(a.FriendlyName))
                {
                    MainWindow.Instance.wave.defaultOutput = a;
                    return;
                }
            }
        }

        private void On_30(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Wave.eq[0] = (float)e.NewValue;
            Wave.update = true;
        }

        private void On_100(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Wave.eq[1] = (float)e.NewValue;
            Wave.update = true;
        }

        private void On_400(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Wave.eq[2] = (float)e.NewValue;
            Wave.update = true;
        }

        private void On_1k(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Wave.eq[3] = (float)e.NewValue;
            Wave.update = true;
        }

        private void On_3k(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Wave.eq[4] = (float)e.NewValue;
            Wave.update = true;
        }

        private void On_6k(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Wave.eq[5] = (float)e.NewValue;
            Wave.update = true;
        }

        private void On_10k(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Wave.eq[6] = (float)e.NewValue;
            Wave.update = true;
        }
        private void On_96k(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Wave.eq[7] = (float)e.NewValue;
            Wave.update = true;
        }

        private void FpsSelect_Change(object sender, SelectionChangedEventArgs e)
        {
            //int.TryParse(((ListBoxItem)combo_fps.SelectedItem).Content.ToString(), out int divisor);
            Wave.Fps = 1000 / 120;
        }

        private void On_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }
        internal static bool Loopback;
        private void On_Checked(object sender, RoutedEventArgs e)
        {
            if (!e.Handled) Loopback = (bool)((CheckBox)e.Source).IsChecked;
            if (Loopback)
            {
                Wave.Instance.reader?.Dispose();
                Wave.graph = new BufferedWaveProvider(Wave.LoopCapture.WaveFormat);
                Wave.graph.DiscardOnBufferOverflow = true;
                Wave.LoopCapture.StartRecording();
                return;
            }
            Wave.LoopCapture.StopRecording();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            int.TryParse(((TextBox)sender).Text, out int w);
            Wave.width = w;
        }
        private void On_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = (Label)e.Source;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0));
        }

        private void On_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = (Label)e.Source;
            label.Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150));
        }
        public static bool CircularStyle = false;
        private void check_waveform_Checked(object sender, RoutedEventArgs e)
        {
            CircularStyle = ((CheckBox)sender).IsChecked.Value;
        }

        private void check_style_Checked(object sender, RoutedEventArgs e)
        {
            Wave.style = true;
        }

        private void check_style_Unchecked(object sender, RoutedEventArgs e)
        {
            Wave.style = false;
        }

        private void Auxiliary_Activated(object sender, EventArgs e)
        {
            this.Top = MainWindow.Instance.Top;
            this.Left = MainWindow.Instance.Left + MainWindow.Instance.Width;
        }
    }
}
