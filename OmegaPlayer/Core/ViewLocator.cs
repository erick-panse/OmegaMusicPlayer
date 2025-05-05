using Avalonia.Controls;
using Avalonia.Controls.Templates;
using System;
using OmegaPlayer.Core.ViewModels;

namespace OmegaPlayer.Core
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
            return null;

        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}
