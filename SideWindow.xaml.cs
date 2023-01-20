using System;
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
using NAudio.Dmo;
using NAudio.FileFormats.Mp3;
using NAudio.Wave;
using NAudio.Wave.Compression;

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
            current = playlist.SelectedIndex == -1 ? 0 : playlist.SelectedIndex;
            if (Playlist.Count <= 0) return;
            //BufferedWaveProvider buff = null;
            bool isMp3 = Playlist[current].EndsWith(".mp3");
            if (isMp3)
            {
                DecompressMp3IntoFile("_audio.wav");
            }

            var wave = MainWindow.Instance.wave;
            wave.reader = new AudioFileReader("_audio.wav");
            wave.audioOut?.Dispose();
            wave.audioOut = new WasapiOut(wave.defaultOutput, AudioClientShareMode.Shared, false, 0);
            wave.audioOut.Init(wave.reader);
            wave.audioOut.Play();

            return;
            WriteCurrent(Playlist[current]);
            if (!isMp3)
            { 
                Window.wave.Init(Playlist[current], Window.wave.defaultOutput);
            }
            else
            {
                Window.wave.Init("_audio.wav", Window.wave.defaultOutput);
            }
            //Window.wave.Init(Playlist[current], Window.wave.defaultOutput);
            //playlist.SelectedIndex = -1;
            //}
            //else
            //{
            //    if (Playlist.Count > 0)
            //    {
            //        Window.wave?.audioOut?.Play();
            //        WriteCurrent(Playlist[0]);
            //    }
            //}
        }
        private BufferedWaveProvider DecompressMp3(bool isMp3 = true)
        {
            var _format = Window.wave.defaultOutput.AudioClient.MixFormat;
            WaveFormat format = _format;
            BufferedWaveProvider buff = null;
            if (isMp3)
            {
                Mp3Frame mp3;
                byte[] buffer = null;
                using (Mp3FileReader read = new Mp3FileReader(Playlist[current]))
                {
                    buff = new BufferedWaveProvider(_format) 
                    {
                        BufferLength = (int)read.Length,
                        ReadFully = true
                    };
                    while ((mp3 = Mp3Frame.LoadFromStream(read)) != null)
                    {
                        var m = new WaveFormatCustomMarshaler().MarshalManagedToNative(read.WaveFormat);
                        format = WaveFormat.MarshalFromPtr(m);
                        buffer = new byte[mp3.FrameLength];
                        AcmMp3FrameDecompressor dmo = new AcmMp3FrameDecompressor(format);
                        try
                        {
                            dmo.DecompressFrame(mp3, buffer, 0);
                        }
                        catch 
                        { 
                            if (buffer != null && buffer.Length > 0)
                            {
                                buff.AddSamples(buffer, 0, buffer.Length);
                            }
                            buffer = null;
                            continue;
                        }
                    }
                }
            }
            return buff;
        }
        private void DecompressMp3IntoFile(string file)
        {
            var _format = Window.wave.defaultOutput.AudioClient.MixFormat;
            WaveFormat format = new WaveFormat(_format.SampleRate, _format.BitsPerSample, _format.Channels);
            Mp3Frame mp3;
            byte[] buffer = null;
            //var decompress = Mp3FileReader.CreateAcmFrameDecompressor(mp3Format);
            using (Mp3FileReader read = new Mp3FileReader(Playlist[current], wf => new DmoMp3FrameDecompressor(wf))
            {
                //var mp3Format = read.Mp3WaveFormat;
                var m = new WaveFormatCustomMarshaler().MarshalManagedToNative(read.Mp3WaveFormat);
                var mp3Format = WaveFormat.MarshalFromPtr(m);
                //BufferedWaveProvider buff = new BufferedWaveProvider(format);
                //var newFormat = new WaveFormat(format.SampleRate, format.Channels);
                using (WaveFileWriter write = new WaveFileWriter(file, format))
                {
                    //DmoMp3FrameDecompressor dmo = new DmoMp3FrameDecompressor(read.Mp3WaveFormat);
                    //AcmMp3FrameDecompressor acm = new AcmMp3FrameDecompressor(mp3Format);
                    while ((mp3 = Mp3Frame.LoadFromStream(read)) != null)
                    {
#region Lower volume
                        /*
                        if (buffer != null && buffer.Length > 0)
                        {
                            //byte[] result = buffer;
                            //buff.AddSamples(buffer, 0, buffer.Length);
                            //VolumeWaveProvider16 volume = new VolumeWaveProvider16(buff);
                            //volume.Volume = 0.99f;
                            //volume.Read(result, 0, result.Length - 1);
                            //buff.ClearBuffer();
                            //volume = null;

                            //  Write result
                            //write.Write(buffer, 0, buffer.Length);
                        }*/
#endregion
                        int converted = 0;
                        int resultLength = 0;
                        buffer = new byte[16384 * 4];
                        try
                        {
                            //IMp3FrameDecompressor decompressor = new DmoMp3FrameDecompressor(new Mp3WaveFormat(mp3.SampleRate, 2, mp3.FrameLength, mp3.BitRate));
                            AcmStream acm = new AcmStream(mp3Format, AcmStream.SuggestPcmFormat(mp3Format));
                            var i = mp3.FrameLength % mp3Format.BlockAlign == 0;
                            Array.Copy(mp3.RawData, acm.SourceBuffer, mp3.FrameLength);
                            converted = acm.Convert(mp3.FrameLength, out resultLength);
                            Array.Copy(acm.DestBuffer, 0, buffer, 0, converted);
                            //if (resultLength != mp3.FrameLength)
                            //{
                            //    throw new Exception($"Conversion length is not equal to frame length {resultLength}/{mp3.FrameLength}");
                            //}
                            //converted = decompressor.DecompressFrame(mp3, buffer, 0);
                            //acm.DecompressFrame(mp3, buffer, 0);
                            write.Write(buffer, 0, mp3.FrameLength);
                            write.Flush();
                        }
                        catch (Exception e)
                        {
                            continue;
                        }
                    }
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
