using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using global::OmegaPlayer.Core.Interfaces;

namespace OmegaPlayer.UI.Attached
{
    /// <summary>
    /// Provides attached properties for binding to an ITrackDisplayHost
    /// regardless of the concrete view model type
    /// </summary>
    public static class TrackDisplayHostProperties
    {
        #region Command Properties

        public static readonly AttachedProperty<ICommand> TrackSelectionCommandProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, ICommand>(
                "TrackSelectionCommand",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<ICommand> PlayTrackCommandProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, ICommand>(
                "PlayTrackCommand",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<ICommand> OpenArtistCommandProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, ICommand>(
                "OpenArtistCommand",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<ICommand> OpenAlbumCommandProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, ICommand>(
                "OpenAlbumCommand",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<ICommand> OpenGenreCommandProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, ICommand>(
                "OpenGenreCommand",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<ICommand> ToggleTrackLikeCommandProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, ICommand>(
                "ToggleTrackLikeCommand",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<ICommand> AddToQueueCommandProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, ICommand>(
                "AddToQueueCommand",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<ICommand> AddAsNextTracksCommandProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, ICommand>(
                "AddAsNextTracksCommand",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<ICommand> ShowPlaylistSelectionDialogCommandProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, ICommand>(
                "ShowPlaylistSelectionDialogCommand",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<ICommand> RemoveTracksFromPlaylistCommandProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, ICommand>(
                "RemoveTracksFromPlaylistCommand",
                typeof(TrackDisplayHostProperties));

        #endregion

        #region Drag & Drop Event Handlers

        public static readonly AttachedProperty<object> TrackDragStartedProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, object>(
                "TrackDragStarted",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<object> TrackDragOverProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, object>(
                "TrackDragOver",
                typeof(TrackDisplayHostProperties));

        public static readonly AttachedProperty<object> TrackDropProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, object>(
                "TrackDrop",
                typeof(TrackDisplayHostProperties));

        #endregion

        #region Getter/Setter Methods

        public static ICommand GetTrackSelectionCommand(AvaloniaObject obj) => obj.GetValue(TrackSelectionCommandProperty);
        public static void SetTrackSelectionCommand(AvaloniaObject obj, ICommand value) => obj.SetValue(TrackSelectionCommandProperty, value);

        public static ICommand GetPlayTrackCommand(AvaloniaObject obj) => obj.GetValue(PlayTrackCommandProperty);
        public static void SetPlayTrackCommand(AvaloniaObject obj, ICommand value) => obj.SetValue(PlayTrackCommandProperty, value);

        public static ICommand GetOpenArtistCommand(AvaloniaObject obj) => obj.GetValue(OpenArtistCommandProperty);
        public static void SetOpenArtistCommand(AvaloniaObject obj, ICommand value) => obj.SetValue(OpenArtistCommandProperty, value);

        public static ICommand GetOpenAlbumCommand(AvaloniaObject obj) => obj.GetValue(OpenAlbumCommandProperty);
        public static void SetOpenAlbumCommand(AvaloniaObject obj, ICommand value) => obj.SetValue(OpenAlbumCommandProperty, value);

        public static ICommand GetOpenGenreCommand(AvaloniaObject obj) => obj.GetValue(OpenGenreCommandProperty);
        public static void SetOpenGenreCommand(AvaloniaObject obj, ICommand value) => obj.SetValue(OpenGenreCommandProperty, value);

        public static ICommand GetToggleTrackLikeCommand(AvaloniaObject obj) => obj.GetValue(ToggleTrackLikeCommandProperty);
        public static void SetToggleTrackLikeCommand(AvaloniaObject obj, ICommand value) => obj.SetValue(ToggleTrackLikeCommandProperty, value);

        public static ICommand GetAddToQueueCommand(AvaloniaObject obj) => obj.GetValue(AddToQueueCommandProperty);
        public static void SetAddToQueueCommand(AvaloniaObject obj, ICommand value) => obj.SetValue(AddToQueueCommandProperty, value);

        public static ICommand GetAddAsNextTracksCommand(AvaloniaObject obj) => obj.GetValue(AddAsNextTracksCommandProperty);
        public static void SetAddAsNextTracksCommand(AvaloniaObject obj, ICommand value) => obj.SetValue(AddAsNextTracksCommandProperty, value);

        public static ICommand GetShowPlaylistSelectionDialogCommand(AvaloniaObject obj) => obj.GetValue(ShowPlaylistSelectionDialogCommandProperty);
        public static void SetShowPlaylistSelectionDialogCommand(AvaloniaObject obj, ICommand value) => obj.SetValue(ShowPlaylistSelectionDialogCommandProperty, value);

        public static ICommand GetRemoveTracksFromPlaylistCommand(AvaloniaObject obj) => obj.GetValue(RemoveTracksFromPlaylistCommandProperty);
        public static void SetRemoveTracksFromPlaylistCommand(AvaloniaObject obj, ICommand value) => obj.SetValue(RemoveTracksFromPlaylistCommandProperty, value);

        public static object GetTrackDragStarted(AvaloniaObject obj) => obj.GetValue(TrackDragStartedProperty);
        public static void SetTrackDragStarted(AvaloniaObject obj, object value) => obj.SetValue(TrackDragStartedProperty, value);

        public static object GetTrackDragOver(AvaloniaObject obj) => obj.GetValue(TrackDragOverProperty);
        public static void SetTrackDragOver(AvaloniaObject obj, object value) => obj.SetValue(TrackDragOverProperty, value);

        public static object GetTrackDrop(AvaloniaObject obj) => obj.GetValue(TrackDropProperty);
        public static void SetTrackDrop(AvaloniaObject obj, object value) => obj.SetValue(TrackDropProperty, value);

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper method to find the ITrackDisplayHost in any parent DataContext
        /// </summary>
        public static ITrackDisplayHost FindTrackDisplayHost(Control control)
        {
            var current = control;

            while (current != null)
            {
                if (current.DataContext is ITrackDisplayHost host)
                {
                    return host;
                }

                current = current.Parent as Control;
            }

            return null;
        }

        /// <summary>
        /// Initializes all command properties on a control by finding
        /// the nearest ITrackDisplayHost in the visual tree
        /// </summary>
        public static void InitializeHost(Control control)
        {
            var host = FindTrackDisplayHost(control);
            if (host == null) return;

            // Set all command properties
            SetTrackSelectionCommand(control, host.TrackSelectionCommand);
            SetPlayTrackCommand(control, host.PlayTrackCommand);
            SetOpenArtistCommand(control, host.OpenArtistCommand);
            SetOpenAlbumCommand(control, host.OpenAlbumCommand);
            SetOpenGenreCommand(control, host.OpenGenreCommand);
            SetToggleTrackLikeCommand(control, host.ToggleTrackLikeCommand);
            SetAddToQueueCommand(control, host.AddToQueueCommand);
            SetAddAsNextTracksCommand(control, host.AddAsNextTracksCommand);
            SetShowPlaylistSelectionDialogCommand(control, host.ShowPlaylistSelectionDialogCommand);
            SetRemoveTracksFromPlaylistCommand(control, host.RemoveTracksFromPlaylistCommand);

            // Set event handler delegate references
            SetTrackDragStarted(control, host.HandleTrackDragStarted);
            SetTrackDragOver(control, host.HandleTrackDragOver);
            SetTrackDrop(control, host.HandleTrackDrop);
        }

        #endregion
    }
}