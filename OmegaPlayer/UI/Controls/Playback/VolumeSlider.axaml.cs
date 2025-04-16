using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using OmegaPlayer.Features.Playback.ViewModels;
using System;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.UI;

namespace OmegaPlayer.Controls
{
    public partial class VolumeSlider : TemplatedControl
    {
        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<VolumeSlider, double>(nameof(Minimum), 0.0);

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<VolumeSlider, double>(nameof(Maximum), 1.0);

        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<VolumeSlider, double>(nameof(Value), 0.5, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public double Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => GetValue(MaximumProperty);
            private set => SetValue(MaximumProperty, value);
        }

        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private Thumb _thumb;
        private Control _track;
        private Control _filledTrack;
        private Canvas _canvas;
        private bool _isDragging = false;
        private bool _isPointerOver = false;
        private TrackControlViewModel _trackControlViewModel;
        private IErrorHandlingService _errorHandlingService;
        private bool _isDisposed = false;

        public VolumeSlider()
        {
            // Get error handling service if available
            _errorHandlingService = App.ServiceProvider?.GetService<IErrorHandlingService>();

            Maximum = 1.0;
            PropertyChanged += OnVolumeSliderPropertyChanged;
        }

        private void OnVolumeSliderPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_isDisposed) return;

            if (e.Property == ValueProperty)
            {
                UpdateThumbPosition();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            // Call base implementation
            base.OnApplyTemplate(e);

            // Clean up old event handlers if this is a re-template
            DetachEventHandlers();

            // Find template elements
            _thumb = e.NameScope.Find<Thumb>("PART_Thumb");
            _track = e.NameScope.Find<Control>("PART_Track");
            _filledTrack = e.NameScope.Find<Control>("PART_FilledTrack");
            _canvas = e.NameScope.Find<Canvas>("PART_Canvas");

            _trackControlViewModel = DataContext as TrackControlViewModel;

            if (_track == null || _thumb == null || _filledTrack == null || _canvas == null) return;

            _track.PointerEntered += Track_PointerEnter;
            _track.PointerExited += Track_PointerLeave;
            _track.PointerPressed += Track_PointerPressed;
            _track.PointerMoved += Track_PointerMoved;
            _track.PointerReleased += Track_PointerReleased;

            _filledTrack.PointerEntered += Track_PointerEnter;
            _filledTrack.PointerExited += Track_PointerLeave;
            _filledTrack.PointerPressed += Track_PointerPressed;
            _filledTrack.PointerMoved += Track_PointerMoved;
            _filledTrack.PointerReleased += Track_PointerReleased;

            _thumb.PointerEntered += Track_PointerEnter;
            _thumb.PointerExited += Track_PointerLeave;
            _thumb.DragDelta += Thumb_DragDelta;

            _canvas.PointerCaptureLost += Canvas_PointerCaptureLost;

            // Subscribe to thumb property change for initial position
            _thumb.PropertyChanged += Thumb_PropertyChanged;

            // Update initial thumb position
            UpdateThumbPosition();
        }

        private void Thumb_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (_isDisposed || _thumb == null) return;

            if (e.Property == BoundsProperty && _thumb.Bounds.Width > 0)
            {
                UpdateThumbPosition();
                // Once we've correctly positioned the thumb based on bounds, unsubscribe
                _thumb.PropertyChanged -= Thumb_PropertyChanged;
            }
        }

        private void Track_PointerEnter(object sender, PointerEventArgs e)
        {
            if (_isDisposed || _thumb == null) return;

            _isPointerOver = true;
            _thumb.IsVisible = true;
        }

        private void Track_PointerLeave(object sender, PointerEventArgs e)
        {
            if (_isDisposed) return;

            _isPointerOver = false;
            if (_thumb != null && !_isDragging)
            {
                _thumb.IsVisible = false;
            }
        }

        private void Track_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (_isDisposed || _track == null || _thumb == null) return;

            e.Pointer.Capture(_track);
            _isDragging = true;

            var point = e.GetPosition(_track);
            UpdateValueFromPosition(point.X);
        }

        private void Track_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_isDisposed || _track == null || !_isDragging || !_isPointerOver) return;

            // Update the slider value while dragging
            var point = e.GetPosition(_track);
            UpdateValueFromPosition(point.X);
        }

        private void Track_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (_isDisposed || _track == null) return;

            e.Pointer.Capture(null);
            _isDragging = false;
        }

        private void Canvas_PointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            if (_isDisposed) return;

            _isDragging = false;
        }

        private void Thumb_DragDelta(object sender, VectorEventArgs e)
        {
            try
            {
                if (_isDisposed || _track == null || _thumb == null) return;

                double trackWidth = _track.Bounds.Width;
                double thumbWidth = _thumb.Bounds.Width;
                double currentThumbLeft = double.IsNaN(Canvas.GetLeft(_thumb)) ? 0 : Canvas.GetLeft(_thumb);
                double newThumbLeft = currentThumbLeft + e.Vector.X;
                double newRelativePosition = newThumbLeft / (trackWidth - thumbWidth);
                double newValue = Math.Max(Minimum, Math.Min(Maximum, Minimum + newRelativePosition * (Maximum - Minimum)));

                Value = Math.Max(Minimum, Math.Min(Maximum, newRelativePosition));
                UpdateThumbPosition();
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling thumb drag in VolumeSlider",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void UpdateValueFromPosition(double position)
        {
            try
            {
                if (_track == null) return;

                double trackWidth = _track.Bounds.Width;
                double relativePosition = Math.Max(0, Math.Min(1, position / trackWidth));
                double newValue = Minimum + relativePosition * (Maximum - Minimum);

                Value = Math.Max(Minimum, Math.Min(Maximum, relativePosition));
                UpdateThumbPosition();
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error updating value from position in VolumeSlider",
                    ex.Message,
                    ex,
                    false);
            }
        }

        private void UpdateThumbPosition()
        {
            try
            {
                if (_isDisposed || _track == null || _thumb == null || _filledTrack == null) return;

                double thumbHeight = _thumb.Bounds.Height;
                double trackHeight = _track.Bounds.Height;
                double thumbTop = ((trackHeight - thumbHeight) / 2) + 11; // Center the thumb vertically
                Canvas.SetTop(_thumb, thumbTop);

                double trackWidth = _track.Bounds.Width;
                double thumbWidth = _thumb.Bounds.Width;
                double relativePosition = (Value - Minimum) / (Maximum - Minimum);

                double thumbLeft = relativePosition * (trackWidth - thumbWidth);
                Canvas.SetLeft(_thumb, thumbLeft);

                // Update filled track width
                double newWidth = trackWidth * relativePosition;
                _filledTrack.Width = Math.Max(0, Math.Min(trackWidth, newWidth));
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error updating thumb position in VolumeSlider",
                    ex.Message,
                    ex,
                    false);
            }
        }


        private void DetachEventHandlers()
        {
            // Clean up track event handlers
            if (_track != null)
            {
                _track.PointerEntered -= Track_PointerEnter;
                _track.PointerExited -= Track_PointerLeave;
                _track.PointerPressed -= Track_PointerPressed;
                _track.PointerMoved -= Track_PointerMoved;
                _track.PointerReleased -= Track_PointerReleased;
            }

            // Clean up filled track event handlers
            if (_filledTrack != null)
            {
                _filledTrack.PointerEntered -= Track_PointerEnter;
                _filledTrack.PointerExited -= Track_PointerLeave;
                _filledTrack.PointerPressed -= Track_PointerPressed;
                _filledTrack.PointerMoved -= Track_PointerMoved;
                _filledTrack.PointerReleased -= Track_PointerReleased;
            }

            // Clean up thumb event handlers
            if (_thumb != null)
            {
                _thumb.PointerEntered -= Track_PointerEnter;
                _thumb.PointerExited -= Track_PointerLeave;
                _thumb.DragDelta -= Thumb_DragDelta;
                _thumb.PropertyChanged -= Thumb_PropertyChanged;
            }

            // Clean up canvas event handlers
            if (_canvas != null)
            {
                _canvas.PointerCaptureLost -= Canvas_PointerCaptureLost;
            }

            // Unsubscribe from property changes
            PropertyChanged -= OnVolumeSliderPropertyChanged;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isDisposed = true;

            // Clean up event handlers
            DetachEventHandlers();

            // Release references
            _track = null;
            _thumb = null;
            _filledTrack = null;
            _canvas = null;
            _trackControlViewModel = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}