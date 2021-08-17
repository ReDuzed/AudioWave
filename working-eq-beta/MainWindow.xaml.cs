using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AudioWave
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static MainWindow Instance;
        internal Wave wave;
        internal SideWindow side;
        internal AuxWindow aux;
        public MainWindow()
        {
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
                return;
            }
        }

        internal void On_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            wave.audioOut.Dispose();
            wave.audioOut = null;
            if (wave.reader != null)
                wave.reader.Dispose();
            if (wave.buffer != null)
                wave.buffer.ClearBuffer();
            if (wave.capture != null)
            {
                wave.capture.StopRecording();
                wave.capture.Dispose();
            }
            if (wave.record != null)
                wave.record.Dispose();
            side.Close();
            aux.Close();
            Process.GetCurrentProcess().Kill();
        }
    }
    public class Wave
    {
        internal AudioFileReader reader;
        private float[] data;
        private MainWindow Window;
        internal WasapiOut audioOut;
        public MMDevice defaultOutput;
        public MMDevice defaultInput;
        public WasapiCapture capture;
        public BufferedWaveProvider buffer;
        public bool monitor, once;
        public WasapiOut monitorOut;
        public Wave()
        {
            Window = MainWindow.Instance;
            Display();
        }
        public void Init(string file, MMDevice output)
        {
            reader = new AudioFileReader(file);
            data = _Buffer(0);
            if (audioOut != null)
            {
                audioOut.Dispose();
                audioOut = new WasapiOut(output, AudioClientShareMode.Shared, false, 0);
                Window.wave.audioOut.PlaybackStopped += MainWindow.Instance.side.On_PlaybackStopped;
                audioOut.Init(reader);
                audioOut.Play();
            }
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
            sample = new BufferedWaveProvider(capture.WaveFormat);
            sample.DiscardOnBufferOverflow = true;
            capture.ShareMode = AudioClientShareMode.Shared;
            capture.StartRecording();
            capture.DataAvailable += Capture_DataAvailable;
        }

        public static bool update = false;
        public bool playback;
        private BufferedWaveProvider graph;
        public static float[] eq = new float[8];
        private BiQuadFilter[] filter = new BiQuadFilter[8];
        private BufferedWaveProvider sample;
        private void Capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            float[] read = new float[e.BytesRecorded];
            Buffer.BlockCopy(e.Buffer, 0, read, 0, e.BytesRecorded);
            Update();
            for (int j = 0; j < read.Length; j++)
            {
                for (int n = 0; n < filter.Length; n++)
                {
                    if (filter[n] != null)
                    {
                        read[j] = filter[n].Transform(read[j]);
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
            for (int i = 0; i < filter.Length; i++)
            {
                if (filter[i] == null)
                    filter[i] = BiQuadFilter.PeakingEQ(rate, freq[i], 0.8f, eq[i]);
                else
                    filter[i].SetPeakingEq(rate, freq[i], 0.8f, eq[i]);
            }
            update = false;
        }
        Action method;
        public static int Fps = 1000 / 30;
        private void Display()
        {
            method = delegate ()
            {
                Thread.Sleep(Fps);
                if (reader != null && audioOut.PlaybackState == PlaybackState.Playing || capture != null && capture.CaptureState == CaptureState.Capturing)
                    GenerateImage();
                MainWindow.Instance.graph.Dispatcher.BeginInvoke(method, System.Windows.Threading.DispatcherPriority.Background);
            };
            MainWindow.Instance.graph.Dispatcher.BeginInvoke(method, System.Windows.Threading.DispatcherPriority.Background);
        }
        private void GenerateImage()
        {
            int width = (int)Window.graph.Width;
            int height = (int)Window.graph.Height;
            int stride = width * ((PixelFormats.Bgr24.BitsPerPixel + 7) / 8);
            using (Bitmap bmp = new Bitmap(width, height, stride, System.Drawing.Imaging.PixelFormat.Format24bppRgb, new IntPtr()))
            {
                using (Graphics graphic = Graphics.FromImage(bmp))
                {
                    PointF[] points = new PointF[width];
                    if ((capture != null && capture.CaptureState != CaptureState.Capturing) || (reader != null && audioOut.PlaybackState == PlaybackState.Playing))
                    {
                        data = _Buffer(points.Length);
                        for (int i = 0; i < points.Length; i++)
                        {
                            points[i] = new PointF(i, height / 2 * data[i] + height / 2);
                        }
                    }
                    else
                    {
                        data = LiveBuffer();
                        for (int i = 0; i < points.Length; i++)
                        {
                            points[i] = new PointF(i, height / 2 * data[i] + height / 2);
                        }
                    }
                    graphic.FillRectangle(System.Drawing.Brushes.Black, new System.Drawing.Rectangle(0, 0, width, height));
                    if (points.Length > 0)
                    {
                        graphic.DrawCurve(System.Drawing.Pens.White, points);
                    }
                }
                var bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
                Window.graph.Source = BitmapSource.Create(width, height, 96f, 96f, PixelFormats.Bgr24, BitmapPalettes.BlackAndWhite, bmpData.Scan0, stride * height, stride);
                bmp.UnlockBits(bmpData);
            }
        }
        private float[] LiveBuffer()
        {
            float[] buffer = new float[graph.BufferLength];
            graph.ToSampleProvider().Read(buffer, 0, buffer.Length);
            return buffer;
        }
        private float[] _Buffer(int length)
        {
            long position = reader.Position;
            float[] buffer = new float[length];
            reader.ToSampleProvider().Read(buffer, 0, buffer.Length);
            reader.Position = position;
            return buffer;
        }
    }
}
