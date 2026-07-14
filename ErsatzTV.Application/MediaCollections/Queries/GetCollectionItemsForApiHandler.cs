using ErsatzTV.Core.Domain;
using ErsatzTV.Infrastructure.Data;
using ErsatzTV.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ErsatzTV.Application.MediaCollections;

public class GetCollectionItemsForApiHandler(IDbContextFactory<TvContext> dbContextFactory)
    : IRequestHandler<GetCollectionItemsForApi, Option<List<CollectionItemViewModel>>>
{
    public async Task<Option<List<CollectionItemViewModel>>> Handle(
        GetCollectionItemsForApi request,
        CancellationToken cancellationToken)
    {
        await using TvContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        Option<Collection> maybeCollection = await dbContext.Collections
            .AsNoTracking()
            .Include(c => c.MediaItems)
            .ThenInclude(i => (i as Show).ShowMetadata)
            .Include(c => c.MediaItems)
            .ThenInclude(i => (i as Movie).MovieMetadata)
            .Include(c => c.MediaItems)
            .ThenInclude(i => (i as Season).SeasonMetadata)
            .Include(c => c.MediaItems)
            .ThenInclude(i => (i as Episode).EpisodeMetadata)
            .Include(c => c.MediaItems)
            .ThenInclude(i => (i as Artist).ArtistMetadata)
            .Include(c => c.MediaItems)
            .ThenInclude(i => (i as MusicVideo).MusicVideoMetadata)
            .Include(c => c.MediaItems)
            .ThenInclude(i => (i as OtherVideo).OtherVideoMetadata)
            .Include(c => c.MediaItems)
            .ThenInclude(i => (i as Song).SongMetadata)
            .SelectOneAsync(c => c.Id, c => c.Id == request.CollectionId, cancellationToken);

        return maybeCollection.Map(
            collection => collection.MediaItems
                .Map(ProjectToViewModel)
                .OrderBy(item => item.MediaItemId)
                .ToList());
    }

    private static CollectionItemViewModel ProjectToViewModel(MediaItem item) =>
        item switch
        {
            Show show => new CollectionItemViewModel(show.Id, "Show", Title(show.ShowMetadata?.Map(m => m.Title))),
            Movie movie => new CollectionItemViewModel(
                movie.Id,
                "Movie",
                Title(movie.MovieMetadata?.Map(m => m.Title))),
            Season season => new CollectionItemViewModel(
                season.Id,
                "Season",
                Title(season.SeasonMetadata?.Map(m => m.Title), $"Season {season.SeasonNumber}")),
            Episode episode => new CollectionItemViewModel(
                episode.Id,
                "Episode",
                Title(episode.EpisodeMetadata?.Map(m => m.Title))),
            Artist artist => new CollectionItemViewModel(
                artist.Id,
                "Artist",
                Title(artist.ArtistMetadata?.Map(m => m.Title))),
            MusicVideo musicVideo => new CollectionItemViewModel(
                musicVideo.Id,
                "MusicVideo",
                Title(musicVideo.MusicVideoMetadata?.Map(m => m.Title))),
            OtherVideo otherVideo => new CollectionItemViewModel(
                otherVideo.Id,
                "OtherVideo",
                Title(otherVideo.OtherVideoMetadata?.Map(m => m.Title))),
            Song song => new CollectionItemViewModel(song.Id, "Song", Title(song.SongMetadata?.Map(m => m.Title))),
            _ => new CollectionItemViewModel(item.Id, item.GetType().Name, string.Empty)
        };

    private static string Title(IEnumerable<string> titles, string fallback = "") =>
        titles?.Filter(t => !string.IsNullOrWhiteSpace(t)).HeadOrNone().IfNone(fallback) ?? fallback;
}
