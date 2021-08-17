using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
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

            LoopCapture = new WasapiLoopbackCapture(WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice());
            LoopCapture.DataAvailable += LoopCapture_DataAvailable;
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
        Action method;
        public static int Fps = 1000 / 30;
        private void Display()
        {
            method = delegate ()
            {
                Thread.Sleep(Fps);
                if (reader != null && audioOut.PlaybackState == PlaybackState.Playing || capture != null && capture.CaptureState == CaptureState.Capturing || LoopCapture != null && LoopCapture.CaptureState == CaptureState.Capturing)
                    GenerateImage();
                MainWindow.Instance.graph.Dispatcher.BeginInvoke(method, System.Windows.Threading.DispatcherPriority.Background);
            };
            MainWindow.Instance.graph.Dispatcher.BeginInvoke(method, System.Windows.Threading.DispatcherPriority.Background);
        }
        private void GenerateImage()
        {
            PointF[] oldPoints = new PointF[] { };
            int width = (int)Window.graph.Width;
            int height = (int)Window.graph.Height;
            int stride = width * ((PixelFormats.Bgr24.BitsPerPixel + 7) / 8);
            using (Bitmap bmp = new Bitmap(width, height, stride, System.Drawing.Imaging.PixelFormat.Format24bppRgb, new IntPtr()))
            {
                using (Graphics graphic = Graphics.FromImage(bmp))
                {
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
                            indexArray[2] = i / 2;
                    }
                    int length = indexArray.Max() - indexArray.Min();
                    if (length + indexArray[2] < width)
                        length += indexArray[2];

                    PointF[] points = new PointF[width];
                    if ((capture != null && capture.CaptureState != CaptureState.Capturing) || (reader != null && audioOut.PlaybackState == PlaybackState.Playing))
                    {
                        for (int i = 0; i < points.Length; i += points.Length / Math.Max(length, 1))
                        {
                            float y = height / 2 * data[i] + height / 2;
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
                        }
                        points[points.Length - 1] = points[points.Length - 2];
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
                    if (points.Length > 1)
                    {
                        graphic.DrawCurve(System.Drawing.Pens.White, points);
                        oldPoints = points;
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
