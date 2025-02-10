using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OmegaPlayer.UI.Controls
{
    public partial class GradientColorPicker : UserControl
    {
        public GradientColorPicker()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}