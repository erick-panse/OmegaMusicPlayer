using Avalonia.Controls;
using OmegaPlayer.ViewModels;

namespace OmegaPlayer.Views
{
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();
            ViewModelLocator.AutoWireViewModel(this);
        }
    }
}