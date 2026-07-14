namespace ErsatzTV.Controllers.Api.Models;

public record CreateCollectionRequest(string Name);

public record UpdateCollectionRequest(string Name)
{
    public bool? UseCustomPlaybackOrder { get; init; }
}

/// <summary>
///     Media items to add to a collection, by type. Most collections are just ShowIds.
/// </summary>
public record CollectionItemsRequest
{
    public List<int> MovieIds { get; init; }
    public List<int> ShowIds { get; init; }
    public List<int> SeasonIds { get; init; }
    public List<int> EpisodeIds { get; init; }
    public List<int> ArtistIds { get; init; }
    public List<int> MusicVideoIds { get; init; }
    public List<int> OtherVideoIds { get; init; }
    public List<int> SongIds { get; init; }
    public List<int> ImageIds { get; init; }
    public List<int> RemoteStreamIds { get; init; }
}

public record RemoveCollectionItemsRequest
{
    public List<int> MediaItemIds { get; init; }
}
