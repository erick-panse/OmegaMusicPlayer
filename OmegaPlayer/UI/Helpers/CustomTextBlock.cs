using Avalonia.Controls;
using Avalonia;
using Avalonia.Media;
using System.Threading.Tasks;
using System;
using System.Threading;
using Avalonia.Input;

namespace OmegaPlayer.UI.Controls.Helpers
{
    public class CustomTextBlock : TextBlock, IObserver<AvaloniaPropertyChangedEventArgs>
    {
        private IDisposable? _pointerOverSubscription;
        private CancellationTokenSource? _scrollingCancellationTokenSource;
        private const double ScrollSpeed = 25.0;
        private const int PauseTimeMs = 1000;
        private bool _isForwardDirection = true;
        private double _textWidth;

        public CustomTextBlock()
        {
            RenderTransform = new TranslateTransform();
            _pointerOverSubscription = this.GetPropertyChangedObservable(IsPointerOverProperty).Subscribe(this);
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }

        public void OnNext(AvaloniaPropertyChangedEventArgs value)
        {
            if (value.Property == IsPointerOverProperty)
            {
                CheckOverflowAndStartScrolling();
            }
        }

        private void CheckOverflowAndStartScrolling()
        {
            var parent = Parent as Control;
            var isReallyOverflowing = false;

            if (parent != null)
            {
                isReallyOverflowing = _textWidth > parent.Bounds.Width;
            }

            if (IsPointerOver && isReallyOverflowing)
            {
                StartScrolling();
            }
            else
            {
                StopScrolling();
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            _textWidth = size.Width;
            return size;
        }

        private async void StartScrolling()
        {
            StopScrolling();
            _scrollingCancellationTokenSource = new CancellationTokenSource();
            var token = _scrollingCancellationTokenSource.Token;

            if (RenderTransform is TranslateTransform transform)
            {
                try
                {
                    var parentControl = Parent as Control;
                    var availableWidth = parentControl?.Bounds.Width ?? 0;

                    // Only continue scrolling if text is actually overflowing
                    while (IsPointerOver && _textWidth > availableWidth && !token.IsCancellationRequested)
                    {
                        // Calculate scroll distance based on container width
                        var scrollDistance = _textWidth - availableWidth;

                        if (_isForwardDirection)
                        {
                            // Scroll forward (to the left)
                            await AnimateTransform(transform, 0, -scrollDistance, token);
                            await Task.Delay(PauseTimeMs, token); // Pause at end
                        }
                        else
                        {
                            // Scroll backward (to the right)
                            await AnimateTransform(transform, -scrollDistance, 0, token);
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

            while (Math.Abs(currentPosition - to) > Math.Abs(pixelsPerFrame) &&
                   !token.IsCancellationRequested)
            {
                currentPosition += pixelsPerFrame;
                transform.X = currentPosition;
                await Task.Delay(delayPerFrame, token);
            }

            // Ensure we end exactly at the target position
            if (!token.IsCancellationRequested)
            {
                transform.X = to;
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
            base.OnDetachedFromVisualTree(e);
            _pointerOverSubscription?.Dispose();
            StopScrolling();
        }
    }
}