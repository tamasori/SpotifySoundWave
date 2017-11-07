using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using Un4seen.Bass;
using Un4seen.BassWasapi;

namespace Spotify_SWandCP
{
    public class SoundAnalyzer
    {
        public readonly byte[] SpectrumData;
        public readonly IEnumerable<BASS_WASAPI_DEVICEINFO> Devices;
        private readonly DispatcherTimer DispatcherTimer;      
        private readonly int Channels;         
        private readonly float[] inputBuffer = new float[8192];

        private bool isEnabled;            
        private bool isInitialised;       
        private readonly int CurrentDevice = -1;

        public SoundAnalyzer(int channels)
        {
            DispatcherTimer = new DispatcherTimer();
            DispatcherTimer.Tick += OnDispatcherTimerTick;
            DispatcherTimer.Interval = TimeSpan.FromMilliseconds(25);
            DispatcherTimer.IsEnabled = false;
            isInitialised = false;

            SpectrumData = new byte[channels];
            Channels = channels;
            Devices = new List<BASS_WASAPI_DEVICEINFO>();

            for (int i = 0; i < BassWasapi.BASS_WASAPI_GetDeviceCount(); i++)
            {
                var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                if (device.IsEnabled && device.IsLoopback)
                {
                    ((List<BASS_WASAPI_DEVICEINFO>)Devices).Add(device);

                    if (device.IsDefault)
                    {
                        CurrentDevice = i;
                    }
                }
            }

            if (CurrentDevice == -1)
            {
                CurrentDevice = Devices.Count() - 1;
            }

            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
            bool result = Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);

            if (!result)
            {
                throw new Exception("Init Error");
            }
        }

        public bool IsEnabled
        {
            get
            {
                return isEnabled;
            }
            set
            {
                isEnabled = value;

                if (value)
                {
                    if (isInitialised == false)
                    {
                        isInitialised |= BassWasapi.BASS_WASAPI_Init(CurrentDevice, 0, 0, BASSWASAPIInit.BASS_WASAPI_BUFFER, 1f, 0.05f, Process, IntPtr.Zero);
                    }

                    BassWasapi.BASS_WASAPI_Start();
                }
                else
                {
                    BassWasapi.BASS_WASAPI_Stop(true);
                }

                DispatcherTimer.IsEnabled = value;
            }
        }

        private void OnDispatcherTimerTick(object sender, EventArgs e)
        {
            if (BassWasapi.BASS_WASAPI_GetData(inputBuffer, (int)BASSData.BASS_DATA_FFT8192) < -1)
            {
                return;
            }

            int b0 = 0;

            for (int channel = 0; channel < Channels; channel++)
            {
                float peak = 0;
                int b1 = (int)Math.Pow(2, channel * 10.0 / (Channels - 1));

                if (b1 > 1023)
                {
                    b1 = 1023;
                }

                if (b1 <= b0)
                {
                    b1 = b0 + 1;
                }

                while (b0 < b1)
                {
                    if (peak < inputBuffer[1 + b0])
                    {
                        peak = inputBuffer[1 + b0];
                    }

                    b0++;
                }
                
                SpectrumData[channel] = (byte)Math.Min(255, Math.Max(0, (int)(Math.Sqrt(peak) * 3 * 255 - 4)));
            }

            Tick?.Invoke(this, EventArgs.Empty);
        }

        private int Process(IntPtr buffer, int length, IntPtr user)
        {
            return length;
        }

        public event EventHandler Tick;
    }
}
