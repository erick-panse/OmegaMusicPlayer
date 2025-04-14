using Avalonia;
using Avalonia.Controls;
using OmegaPlayer.Core;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.UI;
using System;
using Microsoft.Extensions.DependencyInjection;

namespace OmegaPlayer.Features.Playback.Views
{
    public partial class TrackControlView : UserControl
    {
        private TrackControlViewModel _trackControlViewModel;
        private IErrorHandlingService _errorHandlingService;

        public TrackControlView()
        {
            _errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();

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
            try
            {
                // Only proceed if all controls are properly loaded and have valid bounds
                if (ImageButton == null || PlaybackControls == null ||
                    TrackInfoContainer == null || MainGrid == null)
                {
                    return;
                }

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

                // Apply Animation Width padding for AlbumTextBlock with null check
                if (AlbumTextBlock != null)
                {
                    AlbumTextBlock.AnimationWidth = finalWidth - 3;
                }
            }
            catch (Exception ex)
            {
                // Log error with specific details about what failed
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error calculating track info width",
                    $"Layout calculation failed: {ex.Message}",
                    ex,
                    false);

                // Fallback to a reasonable fixed width
                if (TrackInfoContainer != null)
                {
                    TrackInfoContainer.Width = 200;
                }
            }
        }

        private void OnUnloaded(object sender, EventArgs e)
        {
            // Clean up event handlers
            if (MainGrid != null)
            {
                MainGrid.PropertyChanged -= MainGrid_PropertyChanged;
            }

            // Stop timer to prevent memory leaks
            _trackControlViewModel?.StopTimer();

            this.Loaded -= OnLoaded;
            this.Unloaded -= OnUnloaded;

        }
    }
}