using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using System;

namespace OmegaMusicPlayer.UI.Controls
{
    /// <summary>
    /// A custom ComboBox that prevents unwanted scrolling by overriding the Avalonia's TryFocusSelectedItem behavior.
    /// </summary>
    public class CustomComboBox : ComboBox
    {
        private Popup? _customPopup;
        private ScrollViewer? _parentScrollViewer;
        private double _originalScrollPosition;
        private bool _overrideFocus = false;
        private bool _isDisposed = false;

        public event EventHandler? DropDownClosed;
        public event EventHandler? DropDownOpened;

        static CustomComboBox()
        {
            // Ensure all style properties are properly inherited
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            // First, find the parent ScrollViewer
            _parentScrollViewer = this.FindAncestorOfType<ScrollViewer>();

            // Record the original scroll position
            if (_parentScrollViewer != null)
            {
                _originalScrollPosition = _parentScrollViewer.Offset.Y;
            }

            // Get the popup from the template
            if (_customPopup != null)
            {
                _customPopup.Opened -= CustomPopupOpened;
                _customPopup.Closed -= CustomPopupClosed;
            }

            // Find popup in template
            _customPopup = e.NameScope.Find<Popup>("PART_Popup");

            // Replace the default handlers with our custom ones
            if (_customPopup != null)
            {
                _customPopup.Opened += CustomPopupOpened;
                _customPopup.Closed += CustomPopupClosed;
            }

            // Continue with default template application
            base.OnApplyTemplate(e);
        }

        private void CustomPopupOpened(object? sender, EventArgs e)
        {
            if (_isDisposed) return;

            // Save scroll position when popup opens
            if (_parentScrollViewer != null)
            {
                _originalScrollPosition = _parentScrollViewer.Offset.Y;
            }

            // Set flag to override focus behavior
            _overrideFocus = true;

            // Update the dropdown items' flow direction
            UpdateComboBoxFlowDirection();

            // Important: Trigger the event but don't call TryFocusSelectedItem
            DropDownOpened?.Invoke(this, EventArgs.Empty);

            // Make sure to restore scroll position after popup opens
            if (_parentScrollViewer != null)
            {
                _parentScrollViewer.Offset = new Vector(_parentScrollViewer.Offset.X, _originalScrollPosition);
            }
        }

        private void CustomPopupClosed(object? sender, EventArgs e)
        {
            if (_isDisposed) return;

            // Restore scroll position when popup closes
            if (_parentScrollViewer != null)
            {
                _parentScrollViewer.Offset = new Vector(_parentScrollViewer.Offset.X, _originalScrollPosition);
            }

            _overrideFocus = false;

            // Trigger the close event
            DropDownClosed?.Invoke(this, EventArgs.Empty);
        }

        // Override this to prevent unwanted focus behavior
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == IsDropDownOpenProperty)
            {
                var isOpen = change.GetNewValue<bool>();

                // Save current scroll position before state changes
                if (_parentScrollViewer != null && isOpen)
                {
                    _originalScrollPosition = _parentScrollViewer.Offset.Y;
                }

                // Set the pseudo-class without calling base which would trigger TryFocusSelectedItem
                PseudoClasses.Set(":dropdownopen", isOpen);

                // Restore scroll position after state change
                if (_parentScrollViewer != null)
                {
                    _parentScrollViewer.Offset = new Vector(_parentScrollViewer.Offset.X, _originalScrollPosition);
                }
            }
            else if (change.Property == SelectedItemProperty && !_overrideFocus)
            {
                // Only update the selection box item, but skip TryFocusSelectedItem
                UpdateSelectionBoxItem(change.NewValue);
            }
            else
            {
                // For all other property changes, call the base implementation
                base.OnPropertyChanged(change);
            }
        }

        // Helper method to update the selection box without triggering scroll
        private void UpdateSelectionBoxItem(object? item)
        {
            if (_isDisposed) return;

            if (item is ContentControl contentControl)
            {
                item = contentControl.Content;
            }

            SelectionBoxItem = item;
        }

        private void UpdateComboBoxFlowDirection()
        {
            if (_isDisposed) return;

            // Get the popup child directly (safer than GetTemplateChildren)
            if (_customPopup?.Child != null)
            {
                // Apply the current flow direction to the popup content
                _customPopup.Child.FlowDirection = this.FlowDirection;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isDisposed = true;

            // Unsubscribe from events to prevent memory leaks
            if (_customPopup != null)
            {
                _customPopup.Opened -= CustomPopupOpened;
                _customPopup.Closed -= CustomPopupClosed;
                _customPopup = null;
            }

            // Clear references
            _parentScrollViewer = null;

            base.OnDetachedFromVisualTree(e);
        }
    }
}