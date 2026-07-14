namespace ErsatzTV.Application.MediaCollections;

/// <summary>
///     One member of a collection. MediaItemId is the id that
///     <see cref="Commands.AddItemsToCollection" /> and <see cref="Commands.RemoveItemsFromCollection" /> operate on,
///     so a client can diff what a collection contains against what it wants it to contain.
/// </summary>
public record CollectionItemViewModel(int MediaItemId, string Kind, string Name);
