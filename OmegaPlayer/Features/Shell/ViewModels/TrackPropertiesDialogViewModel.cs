using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Features.Library.Models;
using System.Linq;
using TagLib;

namespace OmegaPlayer.Features.Shell.ViewModels
{
    public partial class TrackPropertiesDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;
        private readonly TrackDisplayModel _track;

        [ObservableProperty]
        private string _trackTitle;

        [ObservableProperty]
        private string _albumTitle;

        [ObservableProperty]
        private string _artists;

        [ObservableProperty]
        private string _genre;

        [ObservableProperty]
        private string _duration;

        [ObservableProperty]
        private string _releaseDate;

        [ObservableProperty]
        private string _playCount;

        [ObservableProperty]
        private string _filePath;

        [ObservableProperty]
        private string _bitRate;

        [ObservableProperty]
        private string _fileType;

        public TrackPropertiesDialogViewModel(Window dialog, TrackDisplayModel track)
        {
            _dialog = dialog;
            _track = track;

            // Initialize properties
            TrackTitle = track.Title;
            AlbumTitle = track.AlbumTitle;
            Artists = string.Join(", ", track.Artists.Select(a => a.ArtistName));
            Genre = track.Genre;
            Duration = track.Duration.ToString(@"mm\:ss");
            ReleaseDate = track.ReleaseDate.ToString("d");
            PlayCount = track.PlayCount.ToString();
            FilePath = track.FilePath;
            BitRate = track.BitRate.ToString();
            FileType = track.FileType;
        }

        [RelayCommand]
        private void Close()
        {
            _dialog.Close();
        }
    }
}