﻿using System;
using System.Collections.Generic;
using System.IO;
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

using NAudio.CoreAudioApi;
using NAudio.Wave;


namespace AudioWave
{
    /// <summary>
    /// Interaction logic for SideWindow.xaml
    /// </summary>
    public partial class SideWindow : Window
    {
        public static SideWindow Instance;
        private MainWindow Window;
        private int current;
        private bool playing;
        private bool looping;
        internal bool toggled;
        public List<string> Playlist = new List<string>();
        public SideWindow()
        {
            InitializeComponent();
            Instance = this;
            Window = MainWindow.Instance;
            Window.wave.audioOut = new WasapiOut(MainWindow.Instance.wave.defaultOutput, AudioClientShareMode.Shared, false, 0);
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
                if (current++ < Playlist.Count)
                {
                    int index = Math.Min(current, Playlist.Count - 1);
                    WriteCurrent(Playlist[index]);
                    playlist.SelectedIndex = current;
                    Window.wave.Init(Playlist[index], Window.wave.defaultOutput);
                }
                else
                {
                    playing = false;
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
            AuxWindow.Instance.check_loopback.IsChecked = false;

            playing = true;
            toggled = true;
            current = playlist.SelectedIndex;
            if (playlist.SelectedIndex != -1)
            {
                WriteCurrent(Playlist[current]);
                Window.wave.Init(Playlist[current], Window.wave.defaultOutput);
                //playlist.SelectedIndex = -1;
            }
            else
            {
                if (Playlist.Count > 0)
                {
                    Window.wave?.audioOut?.Play();
                    WriteCurrent(Playlist[0]);
                }
            }
        }
        private void WriteCurrent(string name)
        {
            using (StreamWriter sw = new StreamWriter("current.txt") { NewLine = string.Empty })
            {
                name = name.Substring(0, name.LastIndexOf("."));
                if (name.Contains(@"\"))
                    sw.Write(name.Substring(name.LastIndexOf(@"\") + 1));
                else sw.Write(name);
            }
        }
        public void On_Stop(object sender, MouseButtonEventArgs e)
        {
            playing = false;
            if (Window.wave != null && Window.wave.reader != null)
            {
                Window.wave.audioOut.Stop();
                Window.wave.reader.Seek(0, System.IO.SeekOrigin.Begin);
            }
        }

        private void On_Pause(object sender, MouseButtonEventArgs e)
        {
            if (Window.wave?.audioOut?.PlaybackState == PlaybackState.Playing)
            {
                Window.wave?.audioOut?.Pause();
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

        private void On_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Application.Current.Shutdown();
        }

        bool shuffle = false;
        private void On_Shuffle(object sender, MouseButtonEventArgs e)
        {
            shuffle = !shuffle;
            Label label = (Label)e.Source;
            label.Foreground = shuffle ? new SolidColorBrush(Color.FromRgb(0, 0, 0)) : new SolidColorBrush(Color.FromRgb(150, 150, 150));

            var pathList = new string[Playlist.Count];
            var list = new string[playlist.Items.Count];
            for (int n = 0; n < list.Length; n++)
            {
                list[n] = playlist.Items[n].ToString();
                if (list[n].Contains(":"))
                    list[n] = list[n].Substring(list[n].IndexOf(':') + 2);
                pathList[n] = Playlist[n];
            }

            if (!shuffle)
            {
                playlist.Items.Clear();
                Playlist.Clear();
                list = list.OrderBy(t => t).ToArray();
                Playlist = pathList.OrderBy(t => t).ToList();
                for (int n = 0; n < list.Length; n++)
                {
                    playlist.Items.Add(list[n]);
                }
                return;
            }

            playlist.Items.Clear();
            Playlist.Clear();
            string name = "";
            for (int i = 0; i < list.Length; i++)
            {
                while (true)
                {
                    int rand = new System.Random(MainWindow.Seed.GetHashCode()).Next(list.Length);

                    while (playlist.Items.Contains(name = list[rand]))
                    {
                        if ((MainWindow.Seed += 10) >= int.MaxValue - 10)
                            MainWindow.Seed = 1;
                        rand = new System.Random(MainWindow.Seed.GetHashCode()).Next(list.Length);
                        continue;
                    }

                    playlist.Items.Add(name);
                    Playlist.Add(pathList[rand]);
                    break;
                }
            }
        }
    }
}
