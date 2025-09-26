using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaMusicPlayer.Features.Library.Models;
using System.Linq;

namespace OmegaMusicPlayer.Features.Shell.ViewModels
{
    public partial class TrackPropertiesDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;
        private readonly TrackDisplayModel _track;

        [ObservableProperty]
        private string _trackTitle = string.Empty;

        [ObservableProperty]
        private string _albumTitle = string.Empty;

        [ObservableProperty]
        private string _artists = string.Empty;

        [ObservableProperty]
        private string _genre = string.Empty;

        [ObservableProperty]
        private string _duration = "00:00";

        [ObservableProperty]
        private string _releaseDate = string.Empty;

        [ObservableProperty]
        private string _playCount = "0";

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _bitRate = "0";

        [ObservableProperty]
        private string _fileType = string.Empty;

        public TrackPropertiesDialogViewModel(Window dialog, TrackDisplayModel track)
        {
            _dialog = dialog;
            _track = track;

            // Initialize properties
            InitializeProperties();
        }

        private void InitializeProperties()
        {
            TrackTitle = _track?.Title ?? "Unknown Title";
            AlbumTitle = _track?.AlbumTitle ?? "Unknown Album";
            Artists = _track?.Artists != null && _track.Artists.Any()
                ? string.Join(", ", _track.Artists.Select(a => a.ArtistName))
                : "Unknown Artist";
            Genre = string.IsNullOrEmpty(_track?.Genre) ? "Unknown Genre" : _track.Genre;
            Duration = _track?.Duration.ToString(@"mm\:ss") ?? "00:00";
            ReleaseDate = _track?.ReleaseDate != null
                ? _track.ReleaseDate.ToShortDateString()
                : "Unknown";
            PlayCount = _track?.PlayCount.ToString() ?? "0";
            FilePath = _track?.FilePath ?? "Unknown Path";
            BitRate = _track?.BitRate.ToString() ?? "Unknown";
            FileType = _track?.FileType ?? "Unknown";

        }

        [RelayCommand]
        private void Close()
        {
            _dialog.Close();
        }
    }
}