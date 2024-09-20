using System.Collections.Generic;
using System;
using Avalonia.Media.Imaging;

public class TrackDisplayModel
{
    public int TrackID { get; set; }
    public string Title { get; set; }
    public string AlbumTitle { get; set; }
    public List<string> Artists { get; set; } // To store multiple artists
    public TimeSpan Duration { get; set; }
    public string FilePath { get; set; }
    public string CoverPath { get; set; }
    public string Genre { get; set; }
    public DateTime ReleaseDate { get; set; }
    public int PlayCount { get; set; }

    // New property for thumbnail image
    public Bitmap Thumbnail { get; set; }

    // Optional property to track the resolution of the loaded image (e.g., low or high)
    public string ThumbnailSize { get; set; } = "low";
}