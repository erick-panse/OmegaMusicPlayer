using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Services;

namespace OmegaPlayer.Features.Shell.ViewModels
{
    public partial class LyricsDialogViewModel : ObservableObject
    {
        private readonly Window _dialog;
        private readonly TrackDisplayModel _track;
        private readonly LocalizationService _localizationService;

        [ObservableProperty]
        private string _trackLyrics = string.Empty;

        public LyricsDialogViewModel(Window dialog, TrackDisplayModel track, LocalizationService localizationService)
        {
            _dialog = dialog;
            _track = track;
            _localizationService = localizationService;

            // Initialize properties
            InitializeProperties();
        }

        private void InitializeProperties()
        {
            TrackLyrics = _track?.Lyrics ?? _localizationService["NoLyrics"];
        }

        [RelayCommand]
        private void Close()
        {
            _dialog.Close();
        }
    }
}