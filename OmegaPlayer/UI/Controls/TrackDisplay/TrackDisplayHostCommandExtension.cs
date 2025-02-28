using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using OmegaPlayer.Core.Interfaces;

namespace OmegaPlayer.UI.Attached
{
    /// <summary>
    /// A markup extension that binds to commands on the nearest ITrackDisplayHost
    /// </summary>
    public class TrackDisplayHostCommandExtension : MarkupExtension
    {
        public string CommandName { get; set; }

        public TrackDisplayHostCommandExtension(string commandName)
        {
            CommandName = commandName;
        }

        public TrackDisplayHostCommandExtension()
        {
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget provideValueTarget)
            {
                // Get the target object (usually a Button or MenuItem)
                var targetObject = provideValueTarget.TargetObject as AvaloniaObject;
                if (targetObject == null)
                    return null;

                // Get the command property 
                var commandProperty = provideValueTarget.TargetProperty as AvaloniaProperty;
                if (commandProperty == null || commandProperty.PropertyType != typeof(ICommand))
                    return null;

                // Create a binding that finds the nearest ITrackDisplayHost and gets the requested command
                var binding = new CommandBinding(targetObject, commandProperty, CommandName);
                return binding.Value;
            }

            return null;
        }

        /// <summary>
        /// Helper class that creates a binding to a command on the nearest ITrackDisplayHost
        /// </summary>
        private class CommandBinding
        {
            private readonly AvaloniaObject _target;
            private readonly AvaloniaProperty _property;
            private readonly string _commandName;
            private ICommand _value;

            public CommandBinding(AvaloniaObject target, AvaloniaProperty property, string commandName)
            {
                _target = target;
                _property = property;
                _commandName = commandName;

                if (_target is Control control)
                {
                    control.Loaded += OnControlLoaded;

                    if (control.IsLoaded)
                    {
                        UpdateValue();
                    }
                }
            }

            public ICommand Value => _value;

            private void OnControlLoaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
            {
                UpdateValue();
            }

            private void UpdateValue()
            {
                if (_target is Control control)
                {
                    var host = TrackDisplayHostProperties.FindTrackDisplayHost(control);
                    if (host != null)
                    {
                        _value = GetCommandFromHost(host, _commandName);
                        if (_value != null)
                        {
                            try
                            {
                                _target.SetValue(_property, new CommandWrapper(_value));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error setting command: {ex.Message}");
                            }
                        }
                    }
                }
            }

            private ICommand GetCommandFromHost(ITrackDisplayHost host, string commandName)
            {
                return commandName switch
                {
                    nameof(ITrackDisplayHost.TrackSelectionCommand) => host.TrackSelectionCommand,
                    nameof(ITrackDisplayHost.PlayTrackCommand) => host.PlayTrackCommand,
                    nameof(ITrackDisplayHost.OpenArtistCommand) => host.OpenArtistCommand,
                    nameof(ITrackDisplayHost.OpenAlbumCommand) => host.OpenAlbumCommand,
                    nameof(ITrackDisplayHost.OpenGenreCommand) => host.OpenGenreCommand,
                    nameof(ITrackDisplayHost.ToggleTrackLikeCommand) => host.ToggleTrackLikeCommand,
                    nameof(ITrackDisplayHost.AddToQueueCommand) => host.AddToQueueCommand,
                    nameof(ITrackDisplayHost.AddAsNextTracksCommand) => host.AddAsNextTracksCommand,
                    nameof(ITrackDisplayHost.ShowPlaylistSelectionDialogCommand) => host.ShowPlaylistSelectionDialogCommand,
                    nameof(ITrackDisplayHost.RemoveTracksFromPlaylistCommand) => host.RemoveTracksFromPlaylistCommand,
                    _ => null
                };
            }
        }
    }

    // Add this command wrapper class to handle parameter type mismatches
    public class CommandWrapper : ICommand
    {
        private readonly ICommand _originalCommand;

        public CommandWrapper(ICommand originalCommand)
        {
            _originalCommand = originalCommand;
        }

        public bool CanExecute(object parameter)
        {
            return _originalCommand.CanExecute(parameter);
        }

        public void Execute(object parameter)
        {
            try
            {
                _originalCommand.Execute(parameter);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Command execution error: {ex.Message}");
            }
        }

        public event EventHandler CanExecuteChanged
        {
            add { _originalCommand.CanExecuteChanged += value; }
            remove { _originalCommand.CanExecuteChanged -= value; }
        }
    }
}