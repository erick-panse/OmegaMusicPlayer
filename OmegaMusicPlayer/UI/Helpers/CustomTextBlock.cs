using Avalonia.Controls;
using Avalonia;
using Avalonia.Media;
using System.Threading.Tasks;
using System;
using System.Threading;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace OmegaMusicPlayer.UI.Controls.Helpers
{
    /// <summary>
    /// TextBlock that animates / moves the text back and forth on hover.
    /// </summary>
    public class CustomTextBlock : TextBlock, IObserver<AvaloniaPropertyChangedEventArgs>
    {
        // Add a new StyledProperty for AnimationWidth
        public static readonly StyledProperty<double> AnimationWidthProperty =
            AvaloniaProperty.Register<CustomTextBlock, double>(
                nameof(AnimationWidth),
                double.NaN); // Default to NaN to indicate "not set"

        // Property accessor for the new AnimationWidth property
        public double AnimationWidth
        {
            get => GetValue(AnimationWidthProperty);
            set => SetValue(AnimationWidthProperty, value);
        }

        private IDisposable? _pointerOverSubscription;
        private CancellationTokenSource? _scrollingCancellationTokenSource;
        private const double ScrollSpeed = 25.0;
        private const int PauseTimeMs = 1000;
        private bool _isForwardDirection = true;
        private double _textWidth;
        private bool _isDisposed = false;
        private IErrorHandlingService? _errorHandlingService;

        public CustomTextBlock()
        {
            _errorHandlingService = App.ServiceProvider?.GetService<IErrorHandlingService>();

            RenderTransform = new TranslateTransform();
            _pointerOverSubscription = this.GetPropertyChangedObservable(IsPointerOverProperty).Subscribe(this);
        }

        public void OnCompleted() { }
        public void OnError(Exception error)
        {
            _errorHandlingService?.LogError(
                ErrorSeverity.NonCritical,
                "Error in CustomTextBlock property observer",
                error.Message,
                error,
                false);
        }

        public void OnNext(AvaloniaPropertyChangedEventArgs value)
        {
            if (_isDisposed) return;

            if (value.Property == IsPointerOverProperty)
            {
                CheckOverflowAndStartScrolling();
            }
        }

        private void CheckOverflowAndStartScrolling()
        {
            if (_isDisposed) return;

            var availableWidth = GetAvailableWidth();
            var isReallyOverflowing = _textWidth > availableWidth;

            if (IsPointerOver && isReallyOverflowing)
            {
                StartScrolling();
            }
            else
            {
                StopScrolling();
            }
        }

        // New helper method to determine the correct width to use
        private double GetAvailableWidth()
        {
            // First try to use the explicit AnimationWidth if it's set
            if (!double.IsNaN(AnimationWidth) && AnimationWidth > 0)
            {
                return AnimationWidth;
            }

            // Fall back to parent's width if AnimationWidth is not set
            var parent = Parent as Control;
            return parent?.Bounds.Width ?? 0;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            _textWidth = size.Width;
            return size;
        }

        private async void StartScrolling()
        {
            try
            {
                StopScrolling();

                if (_isDisposed) return;

                _scrollingCancellationTokenSource = new CancellationTokenSource();
                var token = _scrollingCancellationTokenSource.Token;

                if (RenderTransform is TranslateTransform transform)
                {
                    try
                    {
                        var availableWidth = GetAvailableWidth();

                        // Only continue scrolling if text is actually overflowing
                        while (IsPointerOver && _textWidth > availableWidth && !token.IsCancellationRequested && !_isDisposed)
                        {
                            double errorMargin = 5;
                            // Calculate scroll distance based on available width
                            var scrollDistance = _textWidth - availableWidth + errorMargin;

                            if (_isForwardDirection)
                            {
                                // Scroll forward (to the left)
                                await AnimateTransform(transform, 0, -scrollDistance, token);
                                if (token.IsCancellationRequested || _isDisposed) break;
                                await Task.Delay(PauseTimeMs, token); // Pause at end
                            }
                            else
                            {
                                // Scroll backward (to the right)
                                await AnimateTransform(transform, -scrollDistance, 0, token);
                                if (token.IsCancellationRequested || _isDisposed) break;
                                await Task.Delay(PauseTimeMs, token); // Pause at start
                            }

                            _isForwardDirection = !_isForwardDirection; // Toggle direction
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Reset position on cancellation
                        transform.X = 0;
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService?.LogError(
                            ErrorSeverity.NonCritical,
                            "Error during text scrolling",
                            ex.Message,
                            ex,
                            false);

                        // Reset position on error
                        transform.X = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to start text scrolling",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private async Task AnimateTransform(TranslateTransform transform,
                                          double from, double to,
                                          CancellationToken token)
        {
            const int framesPerSecond = 60;
            double pixelsPerFrame = ScrollSpeed / framesPerSecond;
            int delayPerFrame = 1000 / framesPerSecond;

            double currentPosition = from;
            double direction = Math.Sign(to - from);
            pixelsPerFrame *= direction;

            try
            {
                while (Math.Abs(currentPosition - to) > Math.Abs(pixelsPerFrame) &&
                       !token.IsCancellationRequested && !_isDisposed)
                {
                    currentPosition += pixelsPerFrame;
                    transform.X = currentPosition;
                    await Task.Delay(delayPerFrame, token);
                }

                // Ensure we end exactly at the target position
                if (!token.IsCancellationRequested && !_isDisposed)
                {
                    transform.X = to;
                }
            }
            catch (TaskCanceledException)
            {
                // This is expected when cancellation occurs - Do nothing
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error during text animation",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void StopScrolling()
        {
            if (_scrollingCancellationTokenSource != null)
            {
                _scrollingCancellationTokenSource.Cancel();
                _scrollingCancellationTokenSource.Dispose();
                _scrollingCancellationTokenSource = null;
            }

            if (RenderTransform is TranslateTransform transform)
            {
                transform.X = 0;
            }
            _isForwardDirection = true;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            // Mark as disposed to prevent further operations
            _isDisposed = true;

            // Clean up resources
            _pointerOverSubscription?.Dispose();
            StopScrolling();

            base.OnDetachedFromVisualTree(e);
        }
    }
}