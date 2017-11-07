using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using Un4seen.Bass;
using Un4seen.BassWasapi;
using System.Windows.Threading;
using SpotifyAPI.Local;
using SpotifyAPI.Local.Enums;
using SpotifyAPI.Local.Models;

namespace Spotify_SWandCP
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SpotifyLocalAPI _spotify;
        public MainWindow()
        {
            InitializeComponent();
            DispatcherTimer dt = new DispatcherTimer();
            analyzer = new Analyzer(leftC, rightC, spek, comboBox);
            analyzer.Enable = true;
            analyzer.DisplayEnable = true;
            _spotify = new SpotifyLocalAPI();
            if (!SpotifyLocalAPI.IsSpotifyRunning())
                SpotifyLocalAPI.RunSpotify();
            if (!SpotifyLocalAPI.IsSpotifyWebHelperRunning())
                SpotifyLocalAPI.RunSpotifyWebHelper();

            if (!_spotify.Connect())
                return; //We need to call Connect before fetching infos, this will handle Auth stuff

            StatusResponse status = _spotify.GetStatus(); //status contains infos
            _spotify.OnTrackChange += _spotify_OnTrackChange;
        }

        private void _spotify_OnTrackChange(object sender, TrackChangeEventArgs e)
        {
            
            cover.Source = BitmapToImageSource(e.NewTrack.GetAlbumArt(AlbumArtSize.Size320));
            MessageBox.Show(cover.Source.Metadata.ToString());
        }
        ImageSource BitmapToImageSource(Bitmap bitmap)
        {
            ImageSourceConverter c = new ImageSourceConverter();
            return (ImageSource)c.ConvertFrom(bitmap);
        }

        Analyzer analyzer;
    }
    internal class Analyzer
    {
        private bool _enable;               //enabled status
        private DispatcherTimer _t;         //timer that refreshes the display
        public float[] _fft;               //buffer for fft data
        private ProgressBar _l, _r;         //progressbars for left and right channel intensity
        private WASAPIPROC _process;        //callback function to obtain data
        private int _lastlevel;             //last output level
        private int _hanctr;                //last output level counter
        public byte[] _spectrumdata;   //spectrum data buffer
        private Spectrum2 _spectrum;         //spectrum dispay control
        private ComboBox _devicelist;       //device list
        private bool _initialized;          //initialized flag
        private int devindex;               //used device index

        private int _lines = 64;            // number of spectrum lines

        //ctor
        public Analyzer(ProgressBar left, ProgressBar right, Spectrum2 spectrum, ComboBox devicelist)
        {
            _fft = new float[8192];
            _lastlevel = 0;
            _hanctr = 0;
            _t = new DispatcherTimer();
            _t.Tick += _t_Tick;
            _t.Interval = TimeSpan.FromMilliseconds(25); //40hz refresh rate//25
            _t.IsEnabled = false;
            _l = left;
            _r = right;
            _l.Minimum = 0;
            _r.Minimum = 0;
            _r.Maximum = (ushort.MaxValue);
            _l.Maximum = (ushort.MaxValue);
            _process = new WASAPIPROC(Process);
            _spectrumdata = new byte[_lines];
            _spectrum = spectrum;
            _devicelist = devicelist;
            _initialized = false;

            Init();
        }

        // Serial port for arduino output


        // flag for display enable
        public bool DisplayEnable { get; set; }

        //flag for enabling and disabling program functionality
        public bool Enable
        {
            get { return _enable; }
            set
            {
                _enable = value;
                if (value)
                {
                    if (!_initialized)
                    {
                        var array = (_devicelist.Items[_devicelist.SelectedIndex] as string).Split(' ');
                        devindex = Convert.ToInt32(array[0]);
                        bool result = BassWasapi.BASS_WASAPI_Init(devindex, 0, 0, BASSWASAPIInit.BASS_WASAPI_BUFFER, 1f, 0.05f, _process, IntPtr.Zero);
                        if (!result)
                        {
                            var error = Bass.BASS_ErrorGetCode();
                            MessageBox.Show(error.ToString());
                        }
                        else
                        {
                            _initialized = true;
                            _devicelist.IsEnabled = false;
                        }
                    }
                    BassWasapi.BASS_WASAPI_Start();
                }
                else BassWasapi.BASS_WASAPI_Stop(true);
                System.Threading.Thread.Sleep(500);
                _t.IsEnabled = value;
            }
        }

        // initialization
        private void Init()
        {
            bool result = false;
            for (int i = 0; i < BassWasapi.BASS_WASAPI_GetDeviceCount(); i++)
            {
                var device = BassWasapi.BASS_WASAPI_GetDeviceInfo(i);
                if (device.IsEnabled && device.IsLoopback)
                {
                    _devicelist.Items.Add(string.Format("{0} - {1}", i, device.name));
                }
            }
            _devicelist.SelectedIndex = 0;
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_UPDATETHREADS, false);
            result = Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
            if (!result) throw new Exception("Init Error");
        }

        //timer 
        private async void _t_Tick(object sender, EventArgs e)
        {
            int ret = BassWasapi.BASS_WASAPI_GetData(_fft, (int)BASSData.BASS_DATA_FFT8192);  //get ch.annel fft data
            if (ret < -1) return;
            int x, y;
            int b0 = 0;

            //computes the spectrum data, the code is taken from a bass_wasapi sample.
            for (x = 0; x < _lines; x++)
            {
                float peak = 0;
                int b1 = (int)Math.Pow(2, x * 10.0 / (_lines - 1));
                if (b1 > 1023) b1 = 1023;
                if (b1 <= b0) b1 = b0 + 1;
                for (; b0 < b1; b0++)
                {
                    if (peak < _fft[1 + b0]) peak = _fft[1 + b0];
                }
                y = (int)(Math.Sqrt(peak) * 3 * 255 - 4);
                if (y > 255) y = 255;
                if (y < 0) y = 0;
                _spectrumdata[x] = (byte)y;
                //Console.Write("{0, 3} ", y);
            }

            if (DisplayEnable) await _spectrum.Set(_spectrumdata);


            int level = BassWasapi.BASS_WASAPI_GetLevel();
            _l.Value = (Utils.LowWord32(level));
            _r.Value = (Utils.HighWord32(level));
            if (level == _lastlevel && level != 0) _hanctr++;
            _lastlevel = level;

            //Required, because some programs hang the output. If the output hangs for a 75ms
            //this piece of code re initializes the output so it doesn't make a gliched sound for long.
            if (_hanctr > 3)
            {
                _hanctr = 0;
                _l.Value = (0);
                _r.Value = (0);
                Free();
                Bass.BASS_Init(0, 44100, BASSInit.BASS_DEVICE_DEFAULT, IntPtr.Zero);
                _initialized = false;
                Enable = true;
            }

        }

        // WASAPI callback, required for continuous recording
        private int Process(IntPtr buffer, int length, IntPtr user)
        {
            return length;
        }

        //cleanup
        public void Free()
        {
            BassWasapi.BASS_WASAPI_Free();
            Bass.BASS_Free();
        }
    }
}
