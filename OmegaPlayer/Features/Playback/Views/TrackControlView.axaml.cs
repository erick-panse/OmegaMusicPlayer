using Avalonia;
using Avalonia.Controls;
using OmegaPlayer.Core;
using OmegaPlayer.Features.Playback.ViewModels;
using System;

namespace OmegaPlayer.Features.Playback.Views
{

    public partial class TrackControlView : UserControl
    {
        private TrackControlViewModel _trackControlViewModel;   

        public TrackControlView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            _trackControlViewModel = (TrackControlViewModel)this.DataContext;

            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
            MainGrid.PropertyChanged += MainGrid_PropertyChanged;
        }

        private void OnLoaded(object? sender, EventArgs e)
        {
            UpdateTrackInfoWidth();
        }

        private void MainGrid_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Grid.BoundsProperty)
            {
                UpdateTrackInfoWidth();
            }
        }

        private void UpdateTrackInfoWidth()
        {
            // Only proceed if all controls are properly loaded and have valid bounds
            if (ImageButton == null || PlaybackControls == null ||
                TrackInfoContainer == null || MainGrid == null)
            {
                return;
            }

            try
            {
                // Get the positions of important controls
                double imageRight = ImageButton.Bounds.X + ImageButton.Bounds.Width + 10; // Image right edge + margin
                double playbackLeft = PlaybackControls.Bounds.X;  // Left edge of playback controls

                // Calculate the absolute maximum available width (space between image and controls)
                double maxAvailableWidth = playbackLeft - imageRight - 20; // 20px safety margin

                // Ensure we never have a negative or overly small width
                maxAvailableWidth = Math.Max(maxAvailableWidth, 100);

                // Set maximum to either the calculated max or 40% of window (whichever is smaller)
                double maxPercentageWidth = this.Bounds.Width * 0.4;
                double finalWidth = Math.Min(maxAvailableWidth, maxPercentageWidth);

                TrackInfoContainer.Width = finalWidth; // Apply the width
                AlbumTextBlock.AnimationWidth = finalWidth - 3; // Apply proper Animation Width for AlbumTextBlock

            }
            catch (Exception ex)
            {
                // Silent error handling - don't crash the app over layout issues
                Console.WriteLine($"Error calculating track info width: {ex.Message}");

                // Fallback to a reasonable fixed width
                TrackInfoContainer.Width = 200;
            }
        }

        private void OnUnloaded(object sender, EventArgs e)
        {
            MainGrid.PropertyChanged -= MainGrid_PropertyChanged;
            _trackControlViewModel?.StopTimer();
            this.Loaded -= OnLoaded;
            this.Unloaded -= OnUnloaded;
        }

    }
}