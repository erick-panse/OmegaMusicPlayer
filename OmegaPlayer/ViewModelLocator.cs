using Avalonia.Controls;
using Avalonia.Controls.Templates;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using OmegaPlayer.ViewModels;
using System;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Views;

namespace OmegaPlayer
{
    public class ViewModelLocator
    {
        // This static method will be responsible for resolving ViewModels from the DI container.
        public static object ResolveViewModel(Type viewModelType)
        {
            // Assuming you have your DI container available (e.g., IServiceProvider or similar)
            return App.ServiceProvider.GetService(viewModelType);
        }

        // This method will handle locating and setting the ViewModel.
        public static void AutoWireViewModel(object view)
        {
            // Assuming the ViewModel is the same type as the View's DataContext with "ViewModel" suffix
            var viewType = view.GetType();
            var viewModelTypeName = viewType.FullName.Replace("View", "ViewModel");

            // Resolve the ViewModel type by its name
            var viewModelType = Type.GetType(viewModelTypeName);

            if (viewModelType != null)
            {
                // Using the DI container to resolve the ViewModel
                var viewModel = ResolveViewModel(viewModelType);

                if (view is Control control)
                {
                    control.DataContext = viewModel;
                }
            }
        }
    }
}
