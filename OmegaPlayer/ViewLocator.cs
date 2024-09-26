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
        public Control? Build(object? data)
        {
            if (data is null)
                return null;

            var name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);


            if (type != null)
            {
                return (Control)Activator.CreateInstance(type);
            }
            return new TextBlock { Text = $"View not resolved: {name} Type: {type.FullName}" };

        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
