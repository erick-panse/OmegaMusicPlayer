using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmegaPlayer.Core;
using OmegaPlayer.Features.Library.ViewModels;
using System;
using System.Diagnostics;

namespace OmegaPlayer.Features.Library.Views
{
    public partial class VirtualizationTestView : UserControl
    {
        private ScrollViewer _scrollViewer;
        private TextBlock _scrollPositionText;
        private TextBlock _frameRateText;
        private DispatcherTimer _fpsTimer;
        private Stopwatch _frameStopwatch = new();
        private int _frameCount;
        private DateTime _lastScrollUpdate = DateTime.Now;

        public VirtualizationTestView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Find controls
            _scrollViewer = this.FindControl<ScrollViewer>("TestScrollViewer");
            _scrollPositionText = this.FindControl<TextBlock>("ScrollPositionText");
            _frameRateText = this.FindControl<TextBlock>("FrameRateText");

            // Initialize FPS monitoring
            _fpsTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _fpsTimer.Tick += UpdateFrameRate;
            _fpsTimer.Start();
            _frameStopwatch.Start();
        }

        private void TestScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_scrollViewer == null) return;

            try
            {
                // Update scroll position percentage
                var scrollPercentage = _scrollViewer.Extent.Height > 0
                    ? (_scrollViewer.Offset.Y / Math.Max(1, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height)) * 100
                    : 0;

                if (_scrollPositionText != null)
                {
                    _scrollPositionText.Text = $"Scroll: {scrollPercentage:F1}%";
                }

                // Count frames for FPS calculation
                _frameCount++;
                _lastScrollUpdate = DateTime.Now;

                // Log scroll performance if we have a ViewModel
                if (DataContext is VirtualizationTestViewModel viewModel)
                {
                    // Measure UI responsiveness during scroll
                    MeasureScrollResponsiveness(viewModel);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in scroll handler: {ex.Message}");
            }
        }

        private async void MeasureScrollResponsiveness(VirtualizationTestViewModel viewModel)
        {
            var responsiveness = await viewModel.MeasureUIResponsiveness();

            // Only log if response time is concerning (>16ms for 60fps)
            if (responsiveness > 16)
            {
                Debug.WriteLine($"UI thread delay during scroll: {responsiveness}ms");
            }
        }

        private void UpdateFrameRate(object sender, EventArgs e)
        {
            if (_frameRateText == null) return;

            try
            {
                var elapsed = _frameStopwatch.Elapsed.TotalSeconds;
                if (elapsed > 0)
                {
                    var fps = _frameCount / elapsed;

                    // Only show FPS if we've been scrolling recently
                    var timeSinceLastScroll = (DateTime.Now - _lastScrollUpdate).TotalSeconds;
                    if (timeSinceLastScroll < 2)
                    {
                        _frameRateText.Text = $"FPS: {fps:F0}";

                        // Color code based on performance
                        if (fps >= 55)
                            _frameRateText.Foreground = Avalonia.Media.Brushes.Green;
                        else if (fps >= 30)
                            _frameRateText.Foreground = Avalonia.Media.Brushes.Orange;
                        else
                            _frameRateText.Foreground = Avalonia.Media.Brushes.Red;
                    }
                    else
                    {
                        _frameRateText.Text = "FPS: --";
                        _frameRateText.Foreground = Avalonia.Media.Brushes.Gray;
                    }
                }

                // Reset counters
                _frameCount = 0;
                _frameStopwatch.Restart();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating frame rate: {ex.Message}");
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // Clean up timer
            _fpsTimer?.Stop();
            _fpsTimer = null;

            _scrollViewer = null;
            _scrollPositionText = null;
            _frameRateText = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}