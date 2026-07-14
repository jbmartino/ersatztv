namespace ErsatzTV.Application.MediaCollections;

/// <summary>
///     The membership of a collection: the media items actually attached to it. This is deliberately not the same as
///     IMediaCollectionRepository.GetItems, which expands a show into its episodes for the playout builder. Membership
///     is what add and remove operate on, so it is what a client needs in order to reconcile.
/// </summary>
public record GetCollectionItemsForApi(int CollectionId) : IRequest<Option<List<CollectionItemViewModel>>>;
