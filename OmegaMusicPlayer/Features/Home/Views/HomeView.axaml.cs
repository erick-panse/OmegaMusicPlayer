using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace OmegaMusicPlayer.Features.Home.Views
{

    public partial class HomeView : UserControl
    {
        private bool _isScrolling;
        private Point _lastPosition;


        public HomeView()
        {
            InitializeComponent();
        }

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is ScrollViewer)
            {
                _isScrolling = true;
                _lastPosition = e.GetPosition(sender as Visual);
                e.Pointer.Capture(sender as IInputElement);
            }
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (_isScrolling && sender is ScrollViewer scrollViewer)
            {
                var currentPosition = e.GetPosition(sender as Visual);
                var delta = _lastPosition - currentPosition;

                scrollViewer.Offset = new Vector(
                    scrollViewer.Offset.X + delta.X,
                    scrollViewer.Offset.Y);

                _lastPosition = currentPosition;
            }
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isScrolling = false;
            if (sender is IInputElement element)
            {
                e.Pointer.Capture(null);
            }
        }


    }
}