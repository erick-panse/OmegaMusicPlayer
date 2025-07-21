using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using OmegaPlayer.Core;
using System;

namespace OmegaPlayer.Features.Configuration.Views
{
    public partial class ConfigView : UserControl
    {
        private double _savedScrollPosition = 0;
        private ScrollViewer _scrollViewer;

        public ConfigView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);

            // Save a reference to the ScrollViewer for easier access
            this.AttachedToVisualTree += ConfigView_AttachedToVisualTree;
        }

        private void ConfigView_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            _scrollViewer = this.FindControl<ScrollViewer>("ConfigScrollViewer");

            // Ensure it won't automatically scroll on focus
            if (_scrollViewer != null)
            {
                _scrollViewer.BringIntoViewOnFocusChange = false;
            }
        }

        // Event handlers for CustomComboBox controls
        private void ComboBox_GotFocus(object sender, GotFocusEventArgs e)
        {
            if (_scrollViewer != null)
            {
                // Save current scroll position
                _savedScrollPosition = _scrollViewer.Offset.Y;
            }
        }

        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (_scrollViewer != null)
            {
                // Immediately restore saved position
                _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, _savedScrollPosition);

                // Attach event handler to restore scroll position if it changes
                _scrollViewer.PropertyChanged += ScrollViewer_PropertyChanged;
            }
        }

        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (_scrollViewer != null)
            {
                // Remove the property change handler
                _scrollViewer.PropertyChanged -= ScrollViewer_PropertyChanged;

                // Restore to the saved position again
                _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, _savedScrollPosition);
            }
        }

        private void ScrollViewer_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            // If the offset changed, restore it to our saved value
            if (e.Property == ScrollViewer.OffsetProperty && sender is ScrollViewer scrollViewer)
            {
                var newOffset = (Vector)e.NewValue;
                if (Math.Abs(newOffset.Y - _savedScrollPosition) > 0.1)
                {
                    scrollViewer.Offset = new Vector(newOffset.X, _savedScrollPosition);
                }
            }
        }
    }
}