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
using NAudio.Wave.SampleProviders;

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
        private bool halt = false;
        bool resample = true;
        public List<string> Playlist = new List<string>();
        public List<AudioData> readList = new List<AudioData>();
        public SideWindow()
        {
            InitializeComponent();
            Window = MainWindow.Instance;
            Owner = Window;
            Instance = this;
            Wave.Instance.audioOut = new WasapiOut(Wave.defaultOutput, AudioClientShareMode.Shared, false, 0);
        }
        public void On_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            //if (halt) return;
            if (looping)
            {
                Window.wave.reader.Seek(0, System.IO.SeekOrigin.Begin);
                Wave.Instance.audioOut.Play();
                return;
            }
            if (playing)
            {
                if (current++ < Playlist.Count)
                {
                    //int index = Math.Min(current, Playlist.Count - 1);
                    //WriteCurrent(Playlist[index]);
                    //halt = true;
                    playlist.SelectedIndex = current;
                    On_Play(sender, null);
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
            dialog.RestoreDirectory = true;
            dialog.Title = "Select Audio Files";
            dialog.InitialDirectory = Environment.GetEnvironmentVariable("USERPROFILE");
            dialog.Filter = "Audio files|*.mp3;*.wav|WAV files (*.wav)|*.wav|MP3 files (*.mp3)|*.mp3";
            dialog.ShowDialog();
            string[] files = dialog.SafeFileNames;
            string[] fullPath = dialog.FileNames;
            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    Playlist.Add(dialog.FileNames[i]);
                    var item = new ListBoxItem();
                    item.Content = files[i].Substring(0, files[i].Length - 4);
                    item.MouseDoubleClick += Item_MouseDoubleClick;
                    playlist.Items.Add(item);

                    string ext = files[i].Substring(files[i].Length - 4);
                    #region LEGACY Preloading all tracks
                    /*
                    MemoryStream memory = null;
                    if (files[i].EndsWith(".mp3"))
                    {
                        memory = DecompressMp3IntoStream(fullPath[i]);
                    }
                    else if (memory == null)
                    {
                        memory = new MemoryStream();
                        var wfr = new WaveFileReader(fullPath[i]);
                        WaveFileWriter.WriteWavFileToStream(memory, wfr);
                        wfr.Dispose();
                    } */
                    #endregion
                    var data = AudioData.NewAudioData(i, SafeFileName(fullPath[i]), fullPath[i], ext, null);
                    if (!readList.Contains(data))
                    {
                        readList.Add(data);
                    }
                    //PreLoadHandler((ListBoxItem)playlist.Items[0]);
                }
            }
            dialog.Dispose();
        }
        WaveFormatConversionProvider Resample(IWaveProvider wave, int samplerate = 44100)
        {
            if (Wave.defaultOutput != null)
            {
                samplerate = Wave.defaultOutput.AudioClient.MixFormat.SampleRate;
            }
            WaveFormat format = new WaveFormat(samplerate, wave.WaveFormat.BitsPerSample, wave.WaveFormat.Channels);
            return new WaveFormatConversionProvider(format, wave);
        }
        private bool PreLoadOne(string file, ref AudioData data)
        {
            if (data.memory != null)
                return false;
            MemoryStream memory = null;
            if (file.EndsWith(".mp3"))
            {
                memory = DecompressMp3IntoStream(file);
            }
            else if (memory == null)
            {
                memory = new MemoryStream();
                var wfr = new WaveFileReader(file);
            RECOURSE:
                if (resample)
                {
                    try
                    {
                        var wfc = Convert(wfr, Wave.defaultOutput.AudioClient.MixFormat);
                        WaveFileWriter.WriteWavFileToStream(memory, wfc);
                    }
                    catch
                    {
                        resample = false;
                        goto RECOURSE;
                    }
                }
                else
                {
                    resample = true;
                    var wfc = Convert(wfr, Wave.defaultOutput.AudioClient.MixFormat);
                    WaveFileWriter.WriteWavFileToStream(memory, wfc);
                }
                wfr.Dispose();
            }
            data.memory = memory;
            return true;
        }
        IWaveProvider Convert(IWaveProvider iwp, WaveFormat wf)
        {
            WaveFormatConversionProvider wfc = null;
            Pcm8BitToSampleProvider pcm8 = null;
            Pcm16BitToSampleProvider pcm16 = null;
            Pcm24BitToSampleProvider pcm24 = null;
            Pcm32BitToSampleProvider pcm32 = null;
            switch (iwp.WaveFormat.BitsPerSample)
            {
                case 8:
                    (wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, wf.Channels), iwp)).Reposition();
                    pcm8 = new Pcm8BitToSampleProvider(wfc);
                    return pcm8.ToWaveProvider16();
                case 16:
                    (wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, wf.Channels), iwp)).Reposition();
                    pcm16 = new Pcm16BitToSampleProvider(wfc);
                    return pcm16.ToWaveProvider16();
                case 24:
                    pcm24 = new Pcm24BitToSampleProvider(iwp);
                    //(wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, 2), pcm24.ToWaveProvider())).Reposition();
                    return pcm24.ToWaveProvider16();
                case 32:
                    pcm32 = new Pcm32BitToSampleProvider(iwp);
                    //(wfc = new WaveFormatConversionProvider(new WaveFormat(wf.SampleRate, 16, 2), pcm32.ToWaveProvider())).Reposition();
                    return pcm32.ToWaveProvider16();
                default:
                    return iwp;
            }
        }
        private void PreLoadNext(AudioData[] next)
        {
            for (int i = 0; i < next.Length; i++)
            {
                if (next[i].Name == null || next[i].memory != null)
                    continue;
                MemoryStream memory = null;
                if (next[i].Ext == ".mp3")
                {
                    memory = DecompressMp3IntoStream(next[i].FullPath);
                }
                else if (memory == null)
                {
                    memory = new MemoryStream();
                    var wfr = new WaveFileReader(next[i].FullPath);
                RECOURSE:
                    if (resample)
                    {
                        try
                        {
                            var wfc = Convert(wfr, Wave.defaultOutput.AudioClient.MixFormat);
                            WaveFileWriter.WriteWavFileToStream(memory, wfc);
                        }
                        catch
                        {
                            resample = false;
                            goto RECOURSE;
                        }
                    }
                    else
                    {
                        var wfc = Convert(wfr, Wave.defaultOutput.AudioClient.MixFormat);
                        WaveFileWriter.WriteWavFileToStream(memory, wfc);
                        resample = true;
                    }
                    wfr.Dispose();
                }
                next[i].memory = memory;
                readList.RemoveAt(next[i].index);
                readList.Insert(next[i].index, next[i]);
            }
            var _list = next.ToList();
        }
        private void PreLoadHandler(ListBoxItem item)
        {
            string name = item.Content.ToString();
            var array = GetNextTracks(name);
            PreLoadNext(array);
            UnloadExtra(array);
        }
        private void UnloadExtra(AudioData[] data)
        {
            readList.ForEach(t => { if (!data.Contains(t)) t.memory?.Dispose(); t.memory = null; });
        }
        private AudioData[] GetNextTracks(string name)
        {
            var _list = new List<AudioData>();
            int index = 0;
            for (int i = 0; i < Playlist.Count; i++)
            {
                if (name == SafeFileName(Playlist[i]))
                {
                    index = i;
                    break;
                }
            }
            var current = readList.FirstOrDefault(t => SafeFileName(t.Name) == name);
            _list.Add(current);
            if (index + 1 < Playlist.Count)
            {
                var next = readList.FirstOrDefault(t => t.Name == SafeFileName(Playlist[index + 1]));
                _list.Add(next);
            }
            if (index + 2 < Playlist.Count)
            {
                var next2 = readList.FirstOrDefault(t => t.Name == SafeFileName(Playlist[index + 2]));
                _list.Add(next2);
            }
            return _list.ToArray();
        }

        private void Item_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PreLoadHandler((ListBoxItem)sender);
            On_Play(null, null);
        }

        private void On_MouseClear(object sender, MouseButtonEventArgs e)
        {
            int index = playlist.SelectedIndex;
            if (index != -1)
            {
                readList[index].memory?.Dispose();
                readList.RemoveAt(index);
                ((ListBoxItem)playlist.Items[index]).MouseDoubleClick -= Item_MouseDoubleClick;
                Playlist.RemoveAt(index);
                playlist.Items.RemoveAt(index);
            }
        }

        private void On_Play(object sender, MouseButtonEventArgs e)
        {
            AuxWindow.Instance.check_loopback.IsChecked = false;
            playing = true;
            toggled = true;
            current = playlist.SelectedIndex == -1 ? 0 : playlist.SelectedIndex;
            if (Playlist.Count <= 0) return;

            AudioData data = default;
            for (int i = 0; i < Playlist.Count; i++)
            {
                if (readList[i].Name == SafeFileName(Playlist[current]))
                {
                    data = readList[i];
                    if (data.memory == null)
                    {
                        PreLoadOne(data.FullPath, ref data);
                    }
                    Task.Factory.StartNew(() =>
                    {
                        if (readList[i + 1].memory == null)
                        {
                            PreLoadHandler((ListBoxItem)playlist.Items[current]);
                        }
                    });
                    break;
                }
            }
            if (data.memory != null)
            {
                try
                {
                    data.memory.Seek(0, SeekOrigin.Begin);
                    Window.wave.Init(data.memory, Wave.defaultOutput);

                    WriteCurrent(readList[current].Name);
                }
                catch
                {
                    data.memory = null;
                    PreLoadOne(data.FullPath, ref data);

                    data.memory.Seek(0, SeekOrigin.Begin);
                    Window.wave.Init(data.memory, Wave.defaultOutput);

                    WriteCurrent(readList[current].Name);
                }
            }
            return;
            #region legacy
            AuxWindow.Instance.check_loopback.IsChecked = false;
            playing = true;
            toggled = true;
            current = playlist.SelectedIndex == -1 ? 0 : playlist.SelectedIndex;
            if (Playlist.Count <= 0) return;

            bool isMp3 = Playlist[current].EndsWith(".mp3");
            if (isMp3)
            {
                DecompressMp3IntoFile("_audio.wav");
            }

            WriteCurrent(Playlist[current]);
            if (!isMp3)
            {
                Window.wave.Init(Playlist[current], Wave.defaultOutput);
            }
            else
            {
                Window.wave.Init("_audio.wav", Wave.defaultOutput);
            }
            halt = false;
            #endregion
        }
        private BufferedWaveProvider DecompressMp3(bool isMp3 = true)
        {
            var _format = Wave.defaultOutput.AudioClient.MixFormat;
            WaveFormat format = _format;
            BufferedWaveProvider buff = null;
            if (isMp3)
            {
                using (Mp3FileReader read = new Mp3FileReader(Playlist[current], wf => new DmoMp3FrameDecompressor(wf)))
                {
                    buff = new BufferedWaveProvider(_format)
                    {
                        BufferLength = (int)read.Length * 3
                    };
                    MemoryStream memory = new MemoryStream();
                    WaveFileWriter.WriteWavFileToStream(memory, read);
                    byte[] buffer = memory.GetBuffer();
                    buff.AddSamples(buffer, 0, buffer.Length);
                }
            }
            return buff;
        }
        private void DecompressMp3IntoFile(string file)
        {
            using (Mp3FileReader read = new Mp3FileReader(Playlist[current], wf => new DmoMp3FrameDecompressor(wf)))
            {
                Window.wave.Stop();
                WaveFileWriter.CreateWaveFile16(file, read.ToSampleProvider());
            }
        }
        private WaveFileReader DecompressMp3IntoReader(string file, string outputFile = "_audio.wav")
        {
            bool success = false;
            using (Mp3FileReader read = new Mp3FileReader(file, wf => new DmoMp3FrameDecompressor(wf)))
            {
                Window.wave.Stop();
                WaveFileWriter.CreateWaveFile16(outputFile, read.ToSampleProvider());
                success = true;
            }
            return success ? new WaveFileReader(outputFile) : null;
        }

        private MemoryStream DecompressMp3IntoStream(string file, bool resample = true, int samplerate = 441000)
        {
            if (Wave.defaultOutput != null)
                samplerate = Wave.defaultOutput.AudioClient.MixFormat.SampleRate;
            Window.wave.Stop();
            bool success = false;
            MemoryStream mem = new MemoryStream();
            using (Mp3FileReader read = new Mp3FileReader(file, wf => new DmoMp3FrameDecompressor(wf)))
            {
                if (resample)
                {
                    var wfc = Resample(read, samplerate);
                    WaveFileWriter.WriteWavFileToStream(mem, wfc);
                    wfc.Dispose();
                }
                else
                {
                    WaveFileWriter.WriteWavFileToStream(mem, read);
                }
                var format = read.WaveFormat;
                byte[] buffer = mem.GetBuffer(); // RIFF.WaveFormatBuffer(format.SampleRate, format.Channels, format.BitsPerSample, 1, mem.GetBuffer());
                mem.Dispose();
                mem = new MemoryStream();
                mem.Write(buffer, 0, buffer.Length);
                success = true;
            }
            return success ? mem : null;
        }
        private string SafeFileName(string file)
        {
            string result = file;
            int index = 0;
            if (file.Contains("\\"))
            {
                result = file.Substring(file.LastIndexOf("\\") + 1);
            }
            else if (file.Contains("/"))
            {
                result = file.Substring(file.LastIndexOf("/") + 1);
            }
            if (file.ToLower().Contains(".mp3"))
            {
                index = result.LastIndexOf(".mp3");
            }
            else if (file.ToLower().Contains(".wav"))
            {
                index = result.LastIndexOf(".wav");
            }
            result = result.Substring(0, index);
            return result;
        }
        private string GetExtension(string file)
        {
            string result = file;
            int index = 0;
            if (file.Contains("\\"))
            {
                result = file.Substring(file.LastIndexOf("\\") + 1);
            }
            else if (file.Contains("/"))
            {
                result = file.Substring(file.LastIndexOf("/") + 1);
            }
            if (file.ToLower().Contains(".mp3"))
            {
                index = result.LastIndexOf(".mp3");
            }
            else if (file.ToLower().Contains(".wav"))
            {
                index = result.LastIndexOf(".wav");
            }
            result = result.Substring(index);
            return result;
        }
        private void WriteCurrent(string name)
        {
            using (StreamWriter sw = new StreamWriter("current.txt") { NewLine = string.Empty })
            {
                if (name.Contains("."))
                {
                    int index = 0;
                    if ((index = name.IndexOf(".")) == name.Length - 4)
                    {
                        name = name.Substring(0, index);
                    }
                }
                if (name.Contains(@"\"))
                {
                    sw.Write(name.Substring(name.LastIndexOf(@"\") + 1));
                }
                else sw.Write(name);
            }
        }
        public void On_Stop(object sender, MouseButtonEventArgs e)
        {
            playing = false;
            if (Window.wave != null && Window.wave.reader != null)
            {
                Wave.Instance.audioOut.Stop();
                Window.wave.reader.Seek(0, System.IO.SeekOrigin.Begin);
            }
        }

        private void On_Pause(object sender, MouseButtonEventArgs e)
        {
            if (Wave.Instance.audioOut?.PlaybackState == PlaybackState.Playing)
            {
                Wave.Instance.audioOut?.Pause();
            }
            else Wave.Instance.audioOut?.Play();
        }

        private void On_Loop(object sender, MouseButtonEventArgs e)
        {
            looping = !looping;
            label_loop.Foreground = looping ? new SolidColorBrush(Color.FromRgb(0, 0, 0)) : new SolidColorBrush(Color.FromRgb(150, 150, 150));
        }

        private void On_Clear(object sender, MouseButtonEventArgs e)
        {
            for (int i = 0; i < playlist.Items.Count; i++)
            {
                ((ListBoxItem)playlist.Items[i]).MouseDoubleClick -= Item_MouseDoubleClick;
                readList[i].memory?.Dispose();
            }

            playlist.Items.Clear();
            Playlist.Clear();
            readList.Clear();
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
            var _nameList = new string[Playlist.Count];

            foreach (ListBoxItem item in playlist.Items)
            {
                item.MouseDoubleClick -= Item_MouseDoubleClick;
            }

            if (!shuffle)
            {
                playlist.Items.Clear();
                Playlist.Clear();
                list = list.OrderBy(t => t).ToArray();
                readList = readList.OrderBy(t => t.Name).ToList();
                Playlist = pathList.OrderBy(t => t).ToList();
                for (int n = 0; n < list.Length; n++)
                {
                    //playlist.Items.Add(list[n]);
                    ListBoxItem item = new ListBoxItem();
                    item.Content = list[n];
                    item.MouseDoubleClick += Item_MouseDoubleClick;
                    playlist.Items.Add(item);
                }
                return;
            }

            playlist.Items.Clear();
            Playlist.Clear();
            List<AudioData> _data = new List<AudioData>();
            string name = "";
            for (int i = 0; i < list.Length; i++)
            {
                while (true)
                {
                    int rand = new System.Random(MainWindow.Seed.GetHashCode()).Next(list.Length);

                    while (_nameList.Contains(name = list[rand]))
                    {
                        if ((MainWindow.Seed += DateTime.Now.Millisecond) >= int.MaxValue - 1000)
                            MainWindow.Seed = 1;
                        rand = new System.Random(MainWindow.Seed).Next(list.Length);
                        continue;
                    }
                    _nameList[i] = name;

                    var first = readList.FirstOrDefault(t => t.Name == name);
                    _data.Add(AudioData.NewAudioData(i, name, pathList[rand], GetExtension(pathList[rand]), first.memory));

                    ListBoxItem item = new ListBoxItem();
                    item.Content = name;
                    item.MouseDoubleClick += Item_MouseDoubleClick;
                    playlist.Items.Add(item);
                    Playlist.Add(pathList[rand]);
                    break;
                }
            }
            for (int i = 0; i < readList.Count; i++)
            {
                readList[i].memory?.Dispose();
            }
            readList.Clear();
            readList = _data;
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            this.Top = Window.Top;
            this.Left = Window.Left - this.Width;
        }

        private void playlist_Drop(object sender, DragEventArgs e)
        {
            string[] fullPath = (string[])e.Data.GetData(DataFormats.FileDrop);
            string[] files = new string[fullPath.Length];
            for (int n = 0; n < fullPath.Length; n++)
            {
                string text = fullPath[n];
                if (text.Contains("\\"))
                    text = text.Substring(text.LastIndexOf("\\") + 1);
                else if (text.Contains("/"))
                    text = text.Substring(text.LastIndexOf("/") + 1);
                //              text = text.Substring(0, text.LastIndexOf('.'));
                files[n] = text;
            }
            if (files.Length > 0)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    Playlist.Add(fullPath[i]);
                    var item = new ListBoxItem();
                    item.Content = files[i].Substring(0, files[i].Length - 4);
                    item.MouseDoubleClick += Item_MouseDoubleClick;
                    playlist.Items.Add(item);

                    string ext = files[i].Substring(files[i].Length - 4);
                    var data = AudioData.NewAudioData(i, SafeFileName(fullPath[i]), fullPath[i], ext, null);
                    if (!readList.Contains(data))
                    {
                        readList.Add(data);
                    }
                }
            }
        }
    }
    public struct AudioData
    {
        private AudioData(int index, string name, string fullPath, string ext, MemoryStream memory)
        {
            this.memory = memory;
            //this.reader = read;
            this.Ext = ext;
            this.Name = name;
            this.FullPath = fullPath;
            this.index = index;
        }
        public static AudioData NewAudioData(int index, string name, string fullPath, string ext, MemoryStream memory)
        {
            return new AudioData(index, name, fullPath, ext, memory);
        }
        public static string SafeFileName(AudioData data)
        {
            string file = data.FullPath;
            string result = "";
            int index = 0;
            if (file.Contains("\\"))
            {
                result = file.Substring(file.LastIndexOf("\\") + 1);
            }
            else if (file.Contains("/"))
            {
                result = file.Substring(file.LastIndexOf("/") + 1);
            }
            if (file.ToLower().Contains(".mp3"))
            {
                index = result.LastIndexOf(".mp3");
            }
            else if (file.ToLower().Contains(".wav"))
            {
                index = result.LastIndexOf(".wav");
            }
            result = result.Substring(0, index);
            return result;
        }
        //public WaveFileReader reader;
        public MemoryStream memory;
        public string Ext;
        public string Name;
        public string FullPath;
        public int index;
        public override string ToString()
        {
            return $"Name: {Name}, index: {index}, Ext: {Ext}";
        }
        public static bool operator ==(AudioData a, AudioData b)
        {
            return a.Name == b.Name && a.Ext == b.Ext;
        }
        public static bool operator !=(AudioData a, AudioData b)
        {
            return a.Name != b.Name || a.Ext != b.Ext;
        }
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
