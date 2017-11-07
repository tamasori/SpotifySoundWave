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
    public class Channel : INotifyPropertyChanged
    {
        private float value;
        public float Value
        {
            get
            {
                return value;
            }
            set
            {
                this.value = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Value"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        SpotifyLocalAPI _spotify;
        private const int Channels = 10;
        private readonly SoundAnalyzer Analyzer = new SoundAnalyzer(Channels);
        private readonly Channel[] Channel = new Channel[Channels];
        public MainWindow()
        {
            InitializeComponent();
            for (int i = 0; i < Channels; i++)
            {
                Channel[i] = new Channel();
            }

            spek.DataContext = new List<Channel>(Channel);

            Analyzer.Tick += Analyzer_Tick;
            Analyzer.IsEnabled = true;
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
        private void Analyzer_Tick(object sender, EventArgs e)
        {
            for (int i = 0; i < Channels; i++)
            {
                Channel[i].Value = Analyzer.SpectrumData[i] / 255f;
            }
        }
        ImageSource BitmapToImageSource(Bitmap bitmap)
        {
            ImageSourceConverter c = new ImageSourceConverter();
            return (ImageSource)c.ConvertFrom(bitmap);
        }
    }
}
