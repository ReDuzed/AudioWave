using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AudioWave
{
    internal class BufferOut : IWavePlayer
    {
        private WasapiOut wasapi;
        private WasapiOut wasapi2;
        public static bool[] Initialized = new bool[2] { false, false };
        
        public BufferOut(MMDevice device, AudioClientShareMode mode, bool useEventSync, int latency)
        {
            wasapi = new WasapiOut(device, mode, useEventSync, latency);
            wasapi2 = new WasapiOut(device, mode, useEventSync, latency);
        }

        public float Volume 
        { 
            get => wasapi2.Volume = wasapi.Volume; 
            set => wasapi2.Volume = wasapi.Volume = value; 
        }

        public PlaybackState PlaybackState => wasapi.PlaybackState;

        public void RegisterPlaybackStopped(EventHandler<StoppedEventArgs> method)
        {
            wasapi.PlaybackStopped += method;
            wasapi2.PlaybackStopped += method;
        }
        public void UnregisterPlaybackStopped(EventHandler<StoppedEventArgs> method)
        {
            wasapi.PlaybackStopped -= method;
            wasapi2.PlaybackStopped -= method;
        }

        public event EventHandler<StoppedEventArgs> PlaybackStopped;

        public void Dispose()
        {  
            wasapi.Dispose();
            wasapi2.Dispose();
        }

        public void Init(IWaveProvider waveProvider, int index)
        {
            if (index % 2 == 0)
                wasapi.Init(waveProvider);
            if (index % 2 == 1)
                wasapi2.Init(waveProvider);
        }

        public void Init(IWaveProvider[] waveProvider)
        {
            wasapi.Init(waveProvider[0]);
            wasapi2.Init(waveProvider[1]);
        }

        public void Init(IWaveProvider waveProvider)
        {
            wasapi.Init(waveProvider);
        }

        public void Pause(int index)
        {
            if (index % 2 == 0)
                wasapi.Pause();
            if (index % 2 == 1)
                wasapi2.Pause();
        }

        public void Pause()
        {
            wasapi.Pause();
            wasapi2.Pause();
        }

        public void Play(int index)
        {
            if (index % 2 == 0)
                wasapi.Play();
            if (index % 2 == 1)
                wasapi2.Play();
        }

        public void Play()
        {
            wasapi.Play();
            wasapi2.Play();

        }

        public void Stop(int index)
        {
            if (index % 2 == 0)
                wasapi.Stop();
            if (index % 2 == 1)
                wasapi2.Stop();
        }

        public void Stop()
        {
            wasapi.Stop();
            wasapi2.Stop();
        }
    }
}
