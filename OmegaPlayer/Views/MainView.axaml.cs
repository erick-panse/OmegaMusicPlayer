using Avalonia.Controls;
using OmegaPlayer.ViewModels;

namespace OmegaPlayer.Views
{
    public partial class MainView : Window
    {
        public MainView(MainViewModel mainViewModel)
        {
            InitializeComponent();
            DataContext = mainViewModel;
        }
    }
}