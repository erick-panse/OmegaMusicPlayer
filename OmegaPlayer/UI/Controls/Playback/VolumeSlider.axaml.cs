using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using OmegaPlayer.Features.Playback.ViewModels;
using System;

namespace OmegaPlayer.Controls
{
    public partial class VolumeSlider : TemplatedControl
    {
        // Properties for binding
        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<VolumeSlider, double>(nameof(Minimum), 0);

        // Maximum is fixed at 100 for volume
        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<VolumeSlider, double>(nameof(Maximum), 100);

        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<VolumeSlider, double>(nameof(Value), 50, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public double Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => GetValue(MaximumProperty);
            private set => SetValue(MaximumProperty, value); // Fixed to 100
        }

        public double Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        private Thumb? _thumb;
        private Control? _track;
        private Control? _filledTrack;
        private Canvas? _canvas;
        private bool _isDragging = false;
        private TrackControlViewModel _trackControlViewModel;

        public VolumeSlider()
        {
            Maximum = 100.0;
        }


        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

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

            _thumb.IsVisible = false;
            _thumb.PointerEntered += Track_PointerEnter;
            _thumb.PointerExited += Track_PointerLeave;
            _thumb.DragDelta += Thumb_DragDelta;
            _thumb.PropertyChanged += Thumb_PropertyChanged;

            _canvas.PointerCaptureLost += Canvas_PointerCaptureLost;

            UpdateThumbPosition();
        }

        private void Thumb_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty && _thumb?.Bounds.Width > 0)
            {
                // Once the thumb's Bounds are properly set, update the thumb position
                UpdateThumbPosition();
                _thumb.PropertyChanged -= Thumb_PropertyChanged; // Unsubscribe after it aligns correctly
            }
        }
        private void Track_PointerEnter(object? sender, PointerEventArgs e)
        {
            if (_thumb != null)
            {
                _thumb.IsVisible = true;
            }
        }

        private void Track_PointerLeave(object? sender, PointerEventArgs e)
        {
            if (_thumb != null && !_isDragging)
            {
                _thumb.IsVisible = false;
            }
        }

        private void Track_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_track == null || _thumb == null) return;

            e.Pointer.Capture(_track);
            _isDragging = true;
            var point = e.GetPosition(_track);
            UpdateValueFromPosition(point.X);
        }

        private void Track_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_track == null || !_isDragging) return;

            var point = e.GetPosition(_track);
            UpdateValueFromPosition(point.X);
        }

        private void Track_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_track == null) return;
            e.Pointer.Capture(null);
            _isDragging = false;
        }

        private void Canvas_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (_track == null) return;
            e.Pointer.Capture(null);
            _isDragging = false;
        }

        private void Thumb_DragDelta(object? sender, VectorEventArgs e)
        {
            if (_track == null || _thumb == null) return;

            double trackWidth = _track.Bounds.Width;
            double thumbWidth = _thumb.Bounds.Width;
            double currentThumbLeft = double.IsNaN(Canvas.GetLeft(_thumb)) ? 0 : Canvas.GetLeft(_thumb);
            double newThumbLeft = currentThumbLeft + e.Vector.X;
            double newRelativePosition = newThumbLeft / (trackWidth - thumbWidth);
            double newValue = (newThumbLeft <= 0) ? Minimum : Minimum + newRelativePosition * (Maximum - Minimum);

            Value = Math.Max(Minimum, Math.Min(Maximum, newValue));

            UpdateThumbPosition();
        }

        private void UpdateValueFromPosition(double position)
        {
            if (_track == null) return;

            double trackWidth = _track.Bounds.Width;
            double relativePosition = position / trackWidth;
            double newValue = (position <= 0) ? Minimum : Minimum + relativePosition * (Maximum - Minimum);

            Value = Math.Max(Minimum, Math.Min(Maximum, newValue));

            UpdateThumbPosition();
        }

        private void UpdateThumbPosition()
        {
            if (_track == null || _thumb == null || _filledTrack == null) return;

            double thumbHeight = _thumb.Bounds.Height;
            double trackHeight = _track.Bounds.Height;
            double thumbTop = ((trackHeight - thumbHeight) / 2) + 11;
            Canvas.SetTop(_thumb, thumbTop);

            double trackWidth = _track.Bounds.Width;
            double thumbWidth = _thumb.Bounds.Width;
            double relativePosition = (Value - Minimum) / (Maximum - Minimum);

            double thumbLeft = relativePosition * (trackWidth - thumbWidth);
            Canvas.SetLeft(_thumb, thumbLeft);

            double newWidth = _track.Bounds.Width * relativePosition;
            _filledTrack.Width = newWidth;

            OnSliderValueChanged();
        }
        private void OnSliderValueChanged()
        {
            if (_trackControlViewModel == null) return;

            float volume = (float)(Value / 100.0);
            _trackControlViewModel.ChangeVolume(volume);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
        }
    }
}