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

using NAudio.Wave;

namespace AudioWave
{
    /// <summary>
    /// Interaction logic for SideWindow.xaml
    /// </summary>
    public partial class SideWindow : Window
    {
        private MainWindow Window;
        private int current;
        private bool playing;
        private bool looping;
        public SideWindow()
        {
            InitializeComponent();
            Window = MainWindow.Instance;
            Window.wave.audioOut = new WasapiOut();
        }
        public void On_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            if (looping)
            {
                Window.wave.reader.Seek(0, System.IO.SeekOrigin.Begin);
                Window.wave.audioOut.Play();
                return;
            }
            if (playing)
            {
                current++;
                if (current < Playlist.Count)
                {
                    Window.wave.Init(Playlist[current]);
                }
            }
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

        public List<string> Playlist = new List<string>();
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Multiselect = true;
            dialog.Title = "Select Audio Files";
            dialog.InitialDirectory = Environment.GetEnvironmentVariable("USERPROFILE");
            dialog.Filter = "Audio files|*.mp3;*.wav|WAV files (*.wav)|*.wav|MP3 files (*.mp3)|*.mp3";
            dialog.ShowDialog();
            string[] files = dialog.SafeFileNames;
            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    Playlist.Add(dialog.FileNames[i]);
                    var item = new ListBoxItem();
                    item.Content = files[i].Substring(0, files[i].Length - 4);
                    playlist.Items.Add(item);
                }
            }
        }

        private void On_MouseClear(object sender, MouseButtonEventArgs e)
        {
            if (playlist.SelectedIndex != -1)
            {
                Playlist.RemoveAt(playlist.SelectedIndex);
                playlist.Items.RemoveAt(playlist.SelectedIndex);
            }
        }

        private void On_Play(object sender, MouseButtonEventArgs e)
        {
            playing = true;
            current = playlist.SelectedIndex;
            if (playlist.SelectedIndex != -1)
            {
                Window.wave.Init(Playlist[playlist.SelectedIndex]);
                playlist.SelectedIndex = -1;
            }
            else
            {
                Window.wave.audioOut.Play();
            }
        }

        private void On_Stop(object sender, MouseButtonEventArgs e)
        {
            playing = false;
            Window.wave.audioOut.Stop();
            Window.wave.reader.Seek(0, System.IO.SeekOrigin.Begin);
        }

        private void On_Pause(object sender, MouseButtonEventArgs e)
        {
            if (Window.wave.audioOut.PlaybackState == PlaybackState.Playing)
            {
                Window.wave.audioOut.Pause();
            }
        }

        private void On_Loop(object sender, MouseButtonEventArgs e)
        {
            looping = !looping;
            label_loop.Foreground = looping ? new SolidColorBrush(Color.FromRgb(0, 0, 0)) : new SolidColorBrush(Color.FromRgb(150, 150, 150));
        }

        private void On_Clear(object sender, MouseButtonEventArgs e)
        {
            playlist.Items.Clear();
            Playlist.Clear();
        }
    }
}
