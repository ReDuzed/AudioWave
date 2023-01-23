using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        public MainWindow()
        {
            ProcessStartInfo info = new ProcessStartInfo(".\\UpdateClient.exe", $"--version 0.1.0.5 --targetexe AudioWave --updateurl https://github.com/ReDuzed/AudioWave/releases/download/ --changelogurl https://raw.githubusercontent.com/ReDuzed/AudioWave/dev/changelog --versionurl https://raw.githubusercontent.com/ReDuzed/AudioWave/dev/version --zipname audio.wave-v --processid {Process.GetCurrentProcess().Id}");
            Process proc = Process.Start(info);
            InitializeComponent();
            Instance = this;
            wave = new Wave();
            wave.defaultOutput = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            
            try
            {
                side = new SideWindow();
                side.Show();
                aux = new AuxWindow();
                aux.Show();
            }
            catch
            {
                Environment.Exit(0);
                return;
            }
        }

        internal void On_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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
    }
    public class Wave
    {
        internal static WaveFileReader reader;
        private float[] data;
        private MainWindow Window;
        public static WasapiOut audioOut;
        public MMDevice defaultOutput;
        public MMDevice defaultInput;
        public static WasapiCapture capture;
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
            if (capture != null)
            { 
                capture.DataAvailable -= Capture_DataAvailable;
                capture.StopRecording();
                capture.Dispose();
            }
            record?.Dispose();
        }
        public static void EqMixing(WaveFileReader read)
        {
            //  Clone active file reader
            long position = read.Position;
            MemoryStream _mem = new MemoryStream();
            read.CopyTo(_mem);
            read.Position = position;
            WaveFileReader clone = new WaveFileReader(_mem);

            //  Retrieve original header
            WaveFileReader _get = clone;
            byte[] header = new byte[44];
            _get.Position = 0;
            _get.Read(header, 0, header.Length);
            
            //  Get samples
            float[] buffer = new float[clone.Length - clone.Position];
            read.ToSampleProvider().Read(buffer, 0, buffer.Length);
            
            //  EQ filter
            if (filter == null) { filter = new BiQuadFilter[read.WaveFormat.Channels, EqData.Data.Length]; }
            for (int j = 0; j < 3; j++)
            {
                for (int band = 0; band < filter.GetLength(1); band++)
                {
                    int ch = j % read.WaveFormat.Channels;
                    if (filter[ch, band] != null)
                    {
                        buffer[j] = filter[ch, band].Transform(buffer[j]);
                    }
                }
            }
            
            //  Init writing to stream
            var mem = new MemoryStream();
            var _memCopy = new MemoryStream();
            var write = new WaveFileWriter(_memCopy, clone.WaveFormat);
            
            //  Write samples and header
            mem.Position = 0;
            mem.Write(header, 0, header.Length);
            write.WriteSamples(buffer, 0, buffer.Length);
            //  Convert samples into byte array
            byte[] byteBuffer = new byte[_memCopy.Length];
            _memCopy.Read(byteBuffer, 0, byteBuffer.Length);
            mem.Write(byteBuffer, 0, byteBuffer.Length);
            mem.Position = 0;

            //  Clearing unused objects
            write.Dispose();
            _get.Dispose();
            clone.Dispose();

            //  Init this stream into the audio output
            Wave.Instance.Init(mem, Wave.Instance.defaultOutput, true, false, read.Position);
        }
        public void Init(WaveFileReader read, MMDevice output)
        {
            _Init(new WaveFileReader(read), output);
        }
        public void Init(string file, MMDevice output)
        {
            _Init(new WaveFileReader(file), output);
        }
        public void Init(Stream stream, MMDevice output, bool dispose = true, bool resetPosition = true, long position = -1)
        {
            if (resetPosition)
            { 
                if (position == -1)
                    stream.Position = 0;
                else stream.Position = position;
            }
            if (dispose) reader?.Dispose();
            _Init(new WaveFileReader(stream), output, position);
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
        private void _Init(WaveFileReader read, MMDevice output, long position = -1)
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
            if (position >= 0)
            { 
                reader.Position = position;
            }
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
            MMDeviceEnumerator enumerator;
            if (!(enumerator = new MMDeviceEnumerator()).HasDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia))
            {
                enumerator.Dispose();
                return;
            }
            try
            {
                if (input == null)
                {
                    input = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                }
                capture = new WasapiCapture(input, false);
            }
            catch 
            {
                enumerator.Dispose();
                return; 
            }
            enumerator.Dispose();

            buffer = new BufferedWaveProvider(capture.WaveFormat);
            buffer.DiscardOnBufferOverflow = true;
            
            graph = new BufferedWaveProvider(capture.WaveFormat);
            graph.DiscardOnBufferOverflow = true;
            
            if (filter == null) 
            { 
                filter = new BiQuadFilter[capture.WaveFormat.Channels, EqData.Data.Length];
            }
            
            capture.ShareMode = AudioClientShareMode.Shared;
            capture.DataAvailable += Capture_DataAvailable;
            capture.StartRecording();
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
        public static BiQuadFilter[,] filter;
        internal static byte[] CaptureData;
        internal static float[] CaptureSamples;
        bool wait = false;
        internal void Capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            if (wait) return;
            wait = true;
            CaptureData = (byte[])e.Buffer.Clone();
            graph.AddSamples(CaptureData, 0, e.BytesRecorded);
            //float[] read = new float[e.BytesRecorded];
            //Array.Copy(buffer, 0, CaptureData, 0, e.BytesRecorded);
            //byte[] buffer = new byte[e.BytesRecorded];
            //Buffer.BlockCopy(read, 0, buffer, 0, e.BytesRecorded);
            Monitor(CaptureData, e.BytesRecorded);
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
            bool flag = false;
            method = delegate (object sender, EventArgs e)
            {
                if (flag) return;
                flag = true;
                //Thread.Sleep(Fps);
                if ((reader != null && audioOut.PlaybackState == PlaybackState.Playing) || capture != null || (LoopCapture != null && LoopCapture.CaptureState == CaptureState.Capturing))
                    GenerateImage();
                //MainWindow.Instance.graph.Dispatcher.BeginInvoke(method, System.Windows.Threading.DispatcherPriority.Render);
                flag = false;
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

                    if (capture != null && capture.CaptureState == CaptureState.Capturing)
                    {
                        data = _Buffer(width, CaptureData);
                        if (data == null || data.Length == 1) 
                            return;
                    }
                    else data = _Buffer(width);

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
                    if ((capture != null && capture.CaptureState == CaptureState.Capturing) || (reader != null && audioOut.PlaybackState == PlaybackState.Playing))
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
                        for (int i = points.Length - 1; i >= 0 ; i--)
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
                        try
                        { 
                            if (AuxWindow.CircularStyle)
                                graphic.DrawLines(pen, points);
                            else graphic.DrawCurve(pen, points);
                        }
                        catch { }
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
            wait = false;
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
        private float[] _Buffer(int width, byte[] array)
        {
            if (array == null) 
                return new float[] { 0f };
            float[] buffer = new float[array.Length / 4];
            for (int i = 0; i < width - 4; i += 4)
            {
                buffer[i] = BitConverter.ToSingle(array, i);
            }
            if (filter == null) { filter = new BiQuadFilter[capture.WaveFormat.Channels, EqData.Data.Length]; }
            for (int j = 0; j < buffer.Length; j++)
            {
                for (int band = 0; band < filter.GetLength(1); band++)
                {
                    int ch = j % capture.WaveFormat.Channels;
                    if (filter[ch, band] != null)
                    {
                        buffer[j] = filter[ch, band].Transform(buffer[j]);
                    }
                }
            }
            return buffer;
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
