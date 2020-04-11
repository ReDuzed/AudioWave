﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using NAudio;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.MediaFoundation;
using NAudio.Dmo.Effect;

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
            side = new SideWindow();
            side.Show();
            aux = new AuxWindow();
            aux.Show();
        }

        private void On_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            side.Close();
            aux.Close();
            wave.audioOut.Dispose();
            if (wave.reader != null)
                wave.reader.Dispose();
            if (wave.buffer != null)
                wave.buffer.ClearBuffer();
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
            data = Buffer(0);
            audioOut.Dispose();
            audioOut = new WasapiOut(output, AudioClientShareMode.Shared, false, 0);
            Window.wave.audioOut.PlaybackStopped += MainWindow.Instance.side.On_PlaybackStopped;
            audioOut.Init(reader);
            audioOut.Play();
        }
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
            capture.ShareMode = AudioClientShareMode.Shared;
            capture.StartRecording();
            capture.DataAvailable += Capture_DataAvailable;
        }

        public bool playback;
        private BufferedWaveProvider graph;
        private void Capture_DataAvailable(object sender, WaveInEventArgs e)
        {
            graph.AddSamples(e.Buffer, 0, e.BytesRecorded);
            if (monitor)
            {
                buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
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
        Action method;
        private void Display()
        {
            method = delegate ()
            {
                Thread.Sleep(1000 / 150);
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
                        data = Buffer(points.Length);
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
                
                Window.graph.Source = BitmapSource.Create(width, height, 96f, 96f, PixelFormats.Bgr24, null, bmpData.Scan0, stride * height, stride);
                bmp.UnlockBits(bmpData);
            }
        }
        private float[] LiveBuffer()
        {
            float[] buffer = new float[graph.BufferLength];
            graph.ToSampleProvider().Read(buffer, 0, buffer.Length);
            return buffer;
        }
        private float[] Buffer(int length)
        {
            long position = reader.Position;
            float[] buffer = new float[length];
            reader.ToSampleProvider().Read(buffer, 0, buffer.Length);
            reader.Position = position;
            return buffer;
        }
    }
}
