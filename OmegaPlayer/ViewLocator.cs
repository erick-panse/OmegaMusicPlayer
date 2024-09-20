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
    public class ViewLocator : IDataTemplate
    {
        private readonly IServiceProvider _serviceProvider;

        public ViewLocator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Control? Build(object? data)
        {
            if (data is null)
                return null;

            var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);

            var temp = _serviceProvider.GetService(type);
            try 
            {
                
                if (type != null)
                {
                    // Attempt to resolve the control via DI
                    //var control = (Control?)_serviceProvider.GetService(type);
                    var control = (Control?)ActivatorUtilities.CreateInstance(_serviceProvider, type);

                    if (control != null)
                    {
                        control.DataContext = data;
                        return control;
                    }
                    else
                    {
                        // Log an error and return a placeholder control if DI fails to resolve the view
                        ShowMessageBox($"View not resolved: {name} Type: {type.FullName}");
                        return new TextBlock { Text = $"View not resolved: {name} Type: {type.FullName}" };
                    }
                } 
            }
            catch (Exception ex)
            {
                ShowMessageBox($"Exception during control creation: {ex.Message}");
            }
            ShowMessageBox($"View type not found: {name}");
            return new TextBlock { Text = "Not Found: " + name };
        }

        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("DI Resolution Result", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync();
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
