using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using OmegaPlayer.ViewModels;
using System;

namespace OmegaPlayer.Controls
{
    public partial class CustomSlider : TemplatedControl
    {
        // Properties for binding
        public static readonly StyledProperty<double> MinimumProperty =
            AvaloniaProperty.Register<CustomSlider, double>(nameof(Minimum), 0);

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<CustomSlider, double>(nameof(Maximum), 100);

        public static readonly StyledProperty<double> ValueProperty =
            AvaloniaProperty.Register<CustomSlider, double>(nameof(Value), 0, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public double Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
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

        public double TempThumbPosition { get; private set; }

        public CustomSlider()
        {
            this.LayoutUpdated += OnLayoutUpdated;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            // Ensure that the control is fully loaded before positioning the thumb
            UpdateThumbPosition();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _thumb = e.NameScope.Find<Thumb>("PART_Thumb");
            _track = e.NameScope.Find<Control>("PART_Track");
            _filledTrack = e.NameScope.Find<Control>("PART_FilledTrack");
            _canvas = e.NameScope.Find<Canvas>("PART_Canvas");
            // Set the thumb as focusable to capture pointer events
            _trackControlViewModel = DataContext is TrackControlViewModel ? DataContext as TrackControlViewModel : null;

            if (_track == null || _thumb == null || _filledTrack == null || _canvas == null) return;

            _track.Focusable = true;
            _track.IsHitTestVisible = true;

            _track.PointerPressed += Track_PointerPressed;
            _track.PointerMoved += Track_PointerMoved;
            _track.PointerReleased += Track_PointerReleased;

            _filledTrack.Focusable = true;
            _filledTrack.IsHitTestVisible = true;

            _filledTrack.PointerPressed += Track_PointerPressed;
            _filledTrack.PointerMoved += Track_PointerMoved;
            _filledTrack.PointerReleased += Track_PointerReleased;

            _thumb.Focusable = true;
            _thumb.IsHitTestVisible = true;

            _thumb.DragDelta += Thumb_DragDelta;

            _canvas.PointerCaptureLost += Canvas_PointerCaptureLost; // Capture lost event


            UpdateThumbPosition();
        }

        private void Track_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_track == null || _thumb == null) return;

            // Capture the pointer when pressed
            e.Pointer.Capture(_track);
            _isDragging = true;

            // Move the thumb to the pointer location initially
            var point = e.GetPosition(_track);
            UpdateValueFromPosition(point.X);

            // Update TempThumbPosition when the pointer is pressed
            TempThumbPosition = Value;

            OnSliderValueChanged();
        }

        private void Track_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (_track == null || !_isDragging) return;

            _isDragging = true;
            // Update the slider value while dragging
            var point = e.GetPosition(_track);
            UpdateValueFromPosition(point.X);

            // Update TempThumbPosition when the pointer moves
            TempThumbPosition = Value;

            OnSliderValueChanged();
        }

        private void Track_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_track == null) return;

            // Release the pointer capture when the mouse button is released
            e.Pointer.Capture(null);
            PlayFromThumbPosition();
        }

        private void Canvas_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (_track == null) return;

            // Release the pointer capture when the mouse button is lost
            e.Pointer.Capture(null);
            PlayFromThumbPosition();
        }

        private void Thumb_DragDelta(object? sender, VectorEventArgs e)
        {
            if (_track == null || _thumb == null) return;

            double trackWidth = _track.Bounds.Width;
            double thumbWidth = _thumb.Bounds.Width;

            // Get the current thumb left position or default to 0 if NaN
            double currentThumbLeft = double.IsNaN(Canvas.GetLeft(_thumb)) ? 0 : Canvas.GetLeft(_thumb);

            // Calculate the new position based on drag offset
            double newThumbLeft = currentThumbLeft + e.Vector.X;

            // Calculate the new value based on the thumb's new position
            double newRelativePosition = newThumbLeft / (trackWidth - thumbWidth);
            double newValue = (newThumbLeft <= 0) ? Minimum : Minimum + newRelativePosition * (Maximum - Minimum);


            // Clamp the value within the slider's bounds
            Value = Math.Max(Minimum, Math.Min(Maximum, newValue));
            // Update TempThumbPosition
            TempThumbPosition = Value;

            UpdateThumbPosition();
            OnSliderValueChanged();
        }

        private void UpdateValueFromPosition(double position)
        {
            if (_track == null || _thumb == null) return;

            double trackWidth = _track.Bounds.Width;
            double relativePosition = position / trackWidth;
            
            // If the position is at the far left, set the value to Minimum directly
            double newValue = (position <= 0) ? Minimum : Minimum + relativePosition * (Maximum - Minimum);

            // Clamp the value within the slider's range
            Value = Math.Max(Minimum, Math.Min(Maximum, newValue));

            UpdateThumbPosition();
        }

        private void UpdateThumbPosition()
        {
            if (_track == null || _thumb == null || _filledTrack == null) return;

            double trackWidth = _track.Bounds.Width;
            double thumbWidth = _thumb.Bounds.Width;
            double relativePosition = (Value - Minimum) / (Maximum - Minimum);

            // Set thumb position based on the value
            double thumbLeft = relativePosition * (trackWidth - thumbWidth);

            // Set the thumb's left position if it's not already set
            Canvas.SetLeft(_thumb, thumbLeft);

            double thumbHeight = _thumb.Bounds.Height;
            double trackHeight = _track.Bounds.Height;
            double thumbTop = ((trackHeight - thumbHeight) / 2) + 11; // Center the thumb vertically
            Canvas.SetTop(_thumb, thumbTop);
            // Update the filled track width based on the thumb's position
            _filledTrack.Width = thumbLeft + thumbWidth / 2; // Adjust for the center of the thumb

        }

        private void OnSliderValueChanged()
        {
            // Logic to seek the track to the new position (based on the thumb value)
            // Call the playback service to seek the track position
            if (_trackControlViewModel == null) return;
            _trackControlViewModel.Seek(TimeSpan.FromSeconds(Value).TotalSeconds);
        }

        private void PlayFromThumbPosition()
        {
            _isDragging = false;
            if (_trackControlViewModel != null)
            {
                _trackControlViewModel.TrackPosition = TimeSpan.FromSeconds(TempThumbPosition);
                _trackControlViewModel.StopSeeking();
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            this.LayoutUpdated -= OnLayoutUpdated;
        }
    }
}