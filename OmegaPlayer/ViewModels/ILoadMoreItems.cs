using System.Windows.Input;

namespace OmegaPlayer.ViewModels
{
    public interface ILoadMoreItems
    {
        ICommand LoadMoreItemsCommand { get; }
    }
}
