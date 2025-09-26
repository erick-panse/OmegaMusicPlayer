using System.Collections.Generic;
using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OmegaMusicPlayer.Features.Library.Services;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using OmegaMusicPlayer.UI;
using Microsoft.Extensions.DependencyInjection;

namespace OmegaMusicPlayer.Features.Library.Models{
    public partial class TrackDisplayModel : ObservableObject
    {
        private readonly IMessenger _messenger;

        public TrackDisplayModel()
        {
            _messenger = App.ServiceProvider.GetRequiredService<IMessenger>();

            // Register to receive like updates
            _messenger.Register<TrackLikeUpdateMessage>(this, HandleLikeUpdate);
            UpdateLikeIcon();
        }

        public Guid InstanceId { get; set; } = Guid.NewGuid();
        public int TrackID { get; set; }
        public string Title { get; set; }
        public int AlbumID { get; set; }
        public string AlbumTitle { get; set; }
        public List<Artists> Artists { get; set; } // To store multiple artists
        public TimeSpan Duration { get; set; }
        public string FilePath { get; set; }
        public string Lyrics { get; set; }
        public string CoverPath { get; set; }
        public int CoverID { get; set; }  
        public string Genre { get; set; }
        public DateTime ReleaseDate { get; set; }
        public int PlayCount { get; set; }
        public int PlaylistPosition { get; set; } = -1;
        public int NowPlayingPosition { get; set; } = -1;
        public int BitRate { get; set; }
        public string FileType { get; set; }
        public DateTime FileCreatedDate { get; set; }    // Maps to CreatedAt from Tracks database
        public DateTime FileModifiedDate { get; set; }   // Maps to UpdatedAt from Tracks database 

        // Optional property to track the resolution of the loaded image (e.g., low or high)
        public string ThumbnailSize { get; set; } = "low";

        [ObservableProperty]
        public Bitmap _thumbnail;

        [ObservableProperty]
        private bool _isPointerOver;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isCurrentlyPlaying;

        [ObservableProperty]
        private bool _isLiked;

        [ObservableProperty]
        private object _likeIcon;

        [ObservableProperty]
        private bool _isBeingDragged;

        [ObservableProperty]
        private bool _showDropIndicator;

        public int Position { get; set; }

        private void HandleLikeUpdate(object recipient, TrackLikeUpdateMessage message)
        {
            if (message.TrackId == TrackID)
            {
                IsLiked = message.IsLiked;
                UpdateLikeIcon();
            }
        }

        private void UpdateLikeIcon()
        {
            // Check if we're on the UI thread
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                // Safe to access resources directly
                LikeIcon = Application.Current?.FindResource(
                    IsLiked ? "LikeOnIcon" : "LikeOffIcon");
            }
            else
            {
                // Defer to UI thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        LikeIcon = Application.Current?.FindResource(
                            IsLiked ? "LikeOnIcon" : "LikeOffIcon");
                    }
                    catch
                    {
                        // Fallback if resource access fails
                        LikeIcon = null;
                    }
                });
            }
        }


        partial void OnIsLikedChanged(bool value)
        {
            UpdateLikeIcon();
        }
    }

}