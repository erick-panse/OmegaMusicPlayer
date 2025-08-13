namespace OmegaPlayer.Core.Enums.LibraryEnums
{
    public enum ViewType
    {
        List,
        Card,
        Image,
        RoundImage
    }

    public enum ContentType
    {
        Home,
        Search,
        Library,
        Artist,
        Album,
        Genre,
        Playlist,
        Folder,
        Config,
        Details,
        NowPlaying,
        Lyrics,
        ImageMode
    }
    public enum SortType
    {
        Name,
        Artist,
        Album,
        Duration,
        Genre,
        TrackCount,
        PlayCount,
        FileCreated,
        FileModified
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }
    public enum RepeatMode
    {
        None,
        All,
        One
    }
}
