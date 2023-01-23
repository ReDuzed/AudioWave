using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AudioWave
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Instance;
        internal Wave wave;
        internal static Wave _Wave => MainWindow.Instance.wave;
        internal SideWindow side;
        internal AuxWindow aux;
        internal static int Seed = 1;
        public static bool RenderColor = false;
        private Process update;
        private bool init = false;
        public MainWindow()
        {
            DateTime previous = (DateTime)Properties.Settings.Default["previous"];
            DateTime week = previous + TimeSpan.FromDays(7);
            if (previous.CompareTo(week) > 0)
            {
                Properties.Settings.Default["previous"] = DateTime.Now;
                if (System.Windows.MessageBox.Show("Program version check for new updates.", "Prompt", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) 
                { 
                    ProcessStartInfo info = new ProcessStartInfo(".\\UpdateClient.exe", $"--version 0.1.0.6 --targetexe AudioWave --updateurl https://github.com/ReDuzed/AudioWave/releases/download/ --changelogurl https://raw.githubusercontent.com/ReDuzed/AudioWave/dev/changelog --versionurl https://raw.githubusercontent.com/ReDuzed/AudioWave/dev/version --zipname audio.wave-v --processid {Process.GetCurrentProcess().Id}");
                    update = Process.Start(info);
                }
            }
            InitializeComponent();
            Instance = this;
            wave = new Wave();
            wave.defaultOutput = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }

        internal void On_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (update != null)
            { 
                update.CloseMainWindow();
                update.Close();
            }
            wave.Stop();
            side.Close();
            aux.Close();
            Environment.Exit(0);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            background.Width = this.ActualWidth;
            background.Height = this.ActualHeight;
            graph.Width = this.ActualWidth - (this.ActualWidth % 20);
            graph.Height = this.ActualHeight;
            float ratio = 800f / 450f;
            this.Width = this.Height * ratio + 20;
        }

        private void Window_LayoutUpdated(object sender, EventArgs e)
        {
            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += (object o, System.Timers.ElapsedEventArgs args) =>
            {
                Dispatcher.Invoke(() =>
                {
                    this.MaxWidth = 1920;
                    this.MaxHeight = 1080;
                    this.Width = 800;
                    this.Height = 450;
                });
                timer.Dispose();
            };
            timer.Start();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (!init)
            {
                try
                {
                    this.Top = (double)Properties.Settings.Default["top"];
                    this.Left = (double)Properties.Settings.Default["left"];
                    side = new SideWindow();
                    side.Show();
                    aux = new AuxWindow();
                    aux.Show();
                }
                catch
                {
                    if (update != null)
                    {
                        update.CloseMainWindow();
                        update.Close();
                    }
                    Environment.Exit(0);
                    return;
                }
                init = true;
            }
            Properties.Settings.Default["top"] = this.Top;
            Properties.Settings.Default["left"] = this.Left;
            Properties.Settings.Default.Save();
        }
    }
    public class Wave
    {
        internal WaveFileReader reader;
        private float[] data;
        private MainWindow Window;
        internal WasapiOut audioOut;
        public MMDevice defaultOutput;
        public MMDevice defaultInput;
        public WasapiCapture capture;
        public BufferedWaveProvider buffer;
        public bool monitor, once;
        public WasapiOut monitorOut;
        internal static int width = 1;
        internal static Wave Instance;
        internal MixingWaveProvider32 mixer = new MixingWaveProvider32();
        public Wave()
        {
            Window = MainWindow.Instance;
            Instance = this;
            Display();

            LoopCapture = new WasapiLoopbackCapture(WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice());
            LoopCapture.DataAvailable += LoopCapture_DataAvailable;
        }
        public void Stop(bool stopDeviceOut = false)
        {
            if (audioOut != null)
            {
                audioOut.PlaybackStopped -= MainWindow.Instance.side.On_PlaybackStopped;
                audioOut.Stop();
            }
            if (stopDeviceOut)
            {
                audioOut?.Dispose();
                audioOut = null;
            }
            reader?.Dispose();
            capture?.StopRecording();
            capture?.Dispose();
            record?.Dispose();
        }
        public void Init(WaveFileReader read, MMDevice output)
        {
            _Init(new WaveFileReader(read), output);
        }
        public void Init(string file, MMDevice output)
        {
            _Init(new WaveFileReader(file), output);
        }
        public void Init(Stream stream, MMDevice output)
        {
            stream.Position = 0;
            _Init(new WaveFileReader(stream), output);
        }
        public void Init(BufferedWaveProvider buff, MMDevice output)
        {
            data = _Buffer(0);
            if (audioOut != null)
            {
                audioOut.PlaybackStopped -= MainWindow.Instance.side.On_PlaybackStopped;
                audioOut.Dispose();
                audioOut = new WasapiOut(output, AudioClientShareMode.Shared, false, 0);
                audioOut.PlaybackStopped += MainWindow.Instance.side.On_PlaybackStopped;
                audioOut.Init(buff);
                audioOut.Play();
            }
        }
        private void _Init(WaveFileReader read, MMDevice output)
        {
            reader = read;
            data = _Buffer(0);
            if (audioOut != null)
            {
                audioOut.PlaybackStopped -= MainWindow.Instance.side.On_PlaybackStopped;
                audioOut.Dispose();
            }
            audioOut = new WasapiOut(output, AudioClientShareMode.Shared, false, 0);
            audioOut.PlaybackStopped += MainWindow.Instance.side.On_PlaybackStopped;
            audioOut.Init(reader);
            audioOut.Play();
        }
        public WaveRecorder record;
        public void InitAux(MMDevice output)
        {
            if (monitorOut != null)
                monitorOut.Dispose();
            try
            {
                monitorOut = new WasapiOut(output, AudioClientShareMode.Shared, false, 0);
            }
            catch
            {
                monitorOut = new WasapiOut();
            }
            if (buffer != null)
                monitorOut.Init(buffer);
        }
        public void InitCapture(MMDevice input)
        {
            if (input == null)
                input = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
            capture = new WasapiCapture(input, false);

            buffer = new BufferedWaveProvider(capture.WaveFormat);
            buffer.DiscardOnBufferOverflow = true;

            graph = new BufferedWaveProvider(capture.WaveFormat);
            graph.DiscardOnBufferOverflow = true;

            filter = new BiQuadFilter[capture.WaveFormat.Channels, 8];

            capture.ShareMode = AudioClientShareMode.Shared;
            capture.StartRecording();
            capture.DataAvailable += Capture_DataAvailable;
        }

        private void LoopCapture_DataAvailable(object sender, WaveInEventArgs e)
        {
            graph.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        internal static WasapiLoopbackCapture LoopCapture;
        public static bool update = false;
        public bool playback;
        internal static BufferedWaveProvider graph;
        public static float[] eq = new float[8];
        private BiQuadFilter[,] filter = new BiQuadFilter[,] { };
        private void Capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            float[] read = new float[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, read, 0, e.BytesRecorded);
            Update();
            for (int j = 0; j < read.Length; j++)
            {
                for (int band = 0; band < filter.GetLength(1); band++)
                {
                    int ch = j % capture.WaveFormat.Channels;
                    if (filter[ch, band] != null)
                    {
                        read[j] = filter[ch, band].Transform(read[j]);
                    }
                }
            }
            byte[] buffer = new byte[e.BytesRecorded];
            Buffer.BlockCopy(read, 0, buffer, 0, e.BytesRecorded);
            graph.AddSamples(buffer, 0, e.BytesRecorded);
            Monitor(buffer, e.BytesRecorded);
        }

        private void Monitor(byte[] Buffer, int length)
        {
            if (monitor)
            {
                buffer.AddSamples(Buffer, 0, length);
                if (playback)
                {
                    if (audioOut.PlaybackState != PlaybackState.Playing)
                    {
                        if (!once)
                        {
                            InitAux(defaultOutput);
                            once = true;
                        }
                        monitorOut.Play();
                        playback = false;
                    }
                    playback = false;
                    return;
                }
            }
            else if (monitorOut != null)
            {
                monitorOut.Stop();
            }
        }
        public byte[] GetSamples(float[] samples, int sampleCount)
        {
            var pcm = new byte[sampleCount * 2];
            int sampleIndex = 0,
                pcmIndex = 0;

            while (sampleIndex < sampleCount)
            {
                var outsample = (short)(samples[sampleIndex] * short.MaxValue);
                pcm[pcmIndex] = (byte)(outsample & 0xff);
                pcm[pcmIndex + 1] = (byte)((outsample >> 8) & 0xff);

                sampleIndex++;
                pcmIndex += 2;
            }
            return pcm;
        }

        private void Update()
        {
            if (!update)
                return;
            int[] freq = new int[]
            {
                100, 200, 400, 800, 1200, 2400, 4800, 9600
            };
            var rate = capture.WaveFormat.SampleRate;
            for (int bandIndex = 0; bandIndex < filter.GetLength(1); bandIndex++)
            {
                for (int n = 0; n < capture.WaveFormat.Channels; n++)
                {
                    if (filter[n, bandIndex] == null)
                        filter[n, bandIndex] = BiQuadFilter.PeakingEQ(rate, freq[bandIndex], 0.8f, eq[bandIndex]);
                    else
                        filter[n, bandIndex].SetPeakingEq(rate, freq[bandIndex], 0.8f, eq[bandIndex]);
                }
            }
            update = false;
        }
        EventHandler method;
        public static int Fps = 1000 / 120;
        public static bool style = false;
        private void Display()
        {
            method = delegate (object sender, EventArgs e)
            {
                //Thread.Sleep(Fps);
                if (reader != null && audioOut.PlaybackState == PlaybackState.Playing || capture != null && capture.CaptureState == CaptureState.Capturing || LoopCapture != null && LoopCapture.CaptureState == CaptureState.Capturing)
                    GenerateImage();
                //MainWindow.Instance.graph.Dispatcher.BeginInvoke(method, System.Windows.Threading.DispatcherPriority.Render);
            };
            new DispatcherTimer(TimeSpan.FromMilliseconds(Fps), DispatcherPriority.Send, method, MainWindow.Instance.Dispatcher);
            //MainWindow.Instance.graph.Dispatcher.BeginInvoke(method, System.Windows.Threading.DispatcherPriority.Render);
        }
        private void GenerateImage()
        {
            PointF[] oldPoints = new PointF[] { };
            int verticalOffY = 15;    //  For moving the entire graph vertically
            int width = (int)Window.graph.Width;
            int height = (int)Window.graph.Height;
            int stride = width * ((PixelFormats.Bgr24.BitsPerPixel + 7) / 8);
            using (Bitmap bmp = new Bitmap(width, height, stride, System.Drawing.Imaging.PixelFormat.Format24bppRgb, new IntPtr()))
            {
                using (Graphics graphic = Graphics.FromImage(bmp))
                {
                    graphic.FillRectangle(System.Drawing.Brushes.Black, new System.Drawing.Rectangle(0, 0, width, height));

                    data = _Buffer(width);

                    float num = data.Max();
                    float num2 = data.Min();
                    float num3 = data.Average();
                    int[] indexArray = new int[3];
                    for (int i = 0; i < data.Length; i++)
                    {
                        if (num == data[i])
                            indexArray[0] = i;
                        if (num2 == data[i])
                            indexArray[1] = i;
                        if (num3 == data[i])
                            indexArray[2] = i;
                    }
                    int length = indexArray.Max() - indexArray.Min();
                    if (length + indexArray[2] < width)
                        length += indexArray[2];

                    PointF[] points = new PointF[width];
                    if ((capture != null && capture.CaptureState != CaptureState.Capturing) || (reader != null && audioOut.PlaybackState == PlaybackState.Playing))
                    {
                        for (int i = 0; i < points.Length; i += points.Length / Math.Max(length, 1))
                        {
                            float y = height / 2 * (float)(data[i] * (style ? Math.Sin((float)i / width * Math.PI) : 1f)) + height / 2;
                            points[i] = new PointF(Math.Min(i, points.Length), y);
                        }
                        PointF begin = new PointF();
                        bool flag = false;
                        int num4 = 0;
                        for (int i = 1; i < points.Length; i++)
                        {
                            if (points[i] == default(PointF) && !flag)
                            {
                                begin = points[i - 1];
                                num4 = i;
                                flag = true;
                            }
                            if ((points[i] != default(PointF) || i == points.Length - 2) && flag)
                            {
                                for (int j = num4; j < i; j++)
                                {
                                    points[j] = new PointF(begin.X, begin.Y);
                                }
                                flag = false;
                            }
                        }
                        for (int i = points.Length - 1; i >= 0; i--)
                        {
                            if (points[i].X == 0f)
                                points[i].X = i;
                            if (points[i].Y == 0f)
                                points[i].Y = points[i - 1].Y;
                            points[i].Y -= verticalOffY;
                        }
                        points[points.Length - 1] = points[points.Length - 2];
                    }
                    else if (reader == null)
                    {
                        //data = LiveBuffer();
                        //for (int i = 0; i < points.Length; i += points.Length / Math.Max(length, 1))
                        for (int i = 0; i < points.Length; i++)
                        {
                            float y = height / 2 * (float)(data[i] * (style ? Math.Sin((float)i / width * Math.PI) : 1f)) + height / 2;
                            points[i] = new PointF(Math.Min(i, points.Length), y);
                        }
                        //for (int i = 0; i < points.Length; i++)
                        //{
                        //    points[i] = new PointF(i, height / 2 * data[i] + height / 2);
                        //}
                    }
                    if (AuxWindow.CircularStyle)
                        points = CircleEffect(points);
                    if (points.Length > 1)
                    {
                        if ((MainWindow.Seed += 10) >= int.MaxValue - 10)
                            MainWindow.Seed = 1;
                        var pen = new System.Drawing.Pen(System.Drawing.Brushes.White);
                        //var pen = Style.CosineColor(System.Drawing.Color.CornflowerBlue, DateTime.Now.Second * 3f);
                        pen.Width = Math.Min(Math.Max(Wave.width, 1), 12);
                        if (AuxWindow.CircularStyle)
                            graphic.DrawLines(pen, points);
                        else graphic.DrawCurve(pen, points);
                        oldPoints = points;
                    }
                }
                //  Render to WPF ImageSource object
                if (MainWindow.RenderColor)
                {
                    stride = (int)width * ((PixelFormats.Bgr24.BitsPerPixel + 7) / 8);
                    var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, (int)width, (int)height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    Window.graph.Source = BitmapSource.Create((int)width, (int)height, 96f, 96f, PixelFormats.Bgr24, null, data.Scan0, stride * height, stride);
                    bmp.UnlockBits(data);
                }
                else
                {
                    var bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
                    Window.graph.Source = BitmapSource.Create(width, height, 96f, 96f, PixelFormats.Bgr24, BitmapPalettes.BlackAndWhite, bmpData.Scan0, stride * height, stride);
                    bmp.UnlockBits(bmpData);
                }
            }
        }
        private float[] LiveBuffer()
        {
            if (graph == null)
            {
                var format = defaultOutput.AudioClient.MixFormat;
                graph = new BufferedWaveProvider(new WaveFormat(format.SampleRate, format.BitsPerSample, format.Channels));
            }
            float[] buffer = new float[graph.BufferLength];
            graph.ToSampleProvider()?.Read(buffer, 0, buffer.Length);
            return buffer;
        }
        private float[] _Buffer(int length)
        {
            if (reader != null)
            {
                try
                {
                    long position = reader == null ? 0 : reader.Position;
                    float[] buffer = new float[length];
                    reader?.ToSampleProvider()?.Read(buffer, 0, buffer.Length);
                    reader.Position = position;
                    return buffer;
                }
                catch
                {
                    return new float[] { 0f };
                }
            }
            else return LiveBuffer();
        }
        private PointF[] CircleEffect(PointF[] points)
        {
            PointF[] output = new PointF[points.Length + 1];
            float fade = 1 / 24f;
            for (int i = 0; i < points.Length; i++)
            {
                bool flagIn = false;
                bool flagOut = false;
                if (flagIn = i < 24)
                    fade += 1 / 24;
                if (flagOut = i >= points.Length - 24)
                    fade -= 1 / 24f;
                float width = (float)this.Window.graph.Width;
                float height = (float)this.Window.graph.Height;
                float num = Math.Min(Math.Max(fade, 0.1f), 1f);
                float centerX = (float)width / 2f;
                float centerY = (float)height / 2f;
                float radius = centerY;
                float x = centerX + (float)(radius / 3f * (data[i] + 1) * (flagIn || flagOut ? num : 1f) * Math.Cos(i / width * Math.PI * 2f));
                float y = centerY + (float)(radius / 3f * (data[i] + 1) * (flagIn || flagOut ? num : 1f) * Math.Sin(i / width * Math.PI * 2f));
                points[i] = new PointF(x, y);
            }
            Array.Copy(points, output, points.Length);
            output[points.Length] = points[0];
            return output;
        }
    }
}
