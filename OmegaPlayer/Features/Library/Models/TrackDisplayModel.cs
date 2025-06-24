using System.Collections.Generic;
using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Services;
using Avalonia;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;

namespace OmegaPlayer.Features.Library.Models{
    public partial class TrackDisplayModel : ViewModelBase
    {
        private readonly IMessenger _messenger;

        public TrackDisplayModel(IMessenger messenger)
        {
            _messenger = messenger;

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
        public string CoverPath { get; set; }
        public int CoverID { get; set; }  
        public string Genre { get; set; }
        public DateTime ReleaseDate { get; set; }
        public int PlayCount { get; set; }
        public int PlaylistPosition { get; set; } = -1;
        public int NowPlayingPosition { get; set; } = -1;
        public int BitRate { get; set; }
        public string FileType { get; set; }
        public Bitmap Thumbnail { get; set; }
        public DateTime FileCreatedDate { get; set; }    // Maps to CreatedAt from Tracks database
        public DateTime FileModifiedDate { get; set; }   // Maps to UpdatedAt from Tracks database 

        // Optional property to track the resolution of the loaded image (e.g., low or high)
        public string ThumbnailSize { get; set; } = "low";

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