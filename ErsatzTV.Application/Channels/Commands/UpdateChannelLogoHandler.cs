using System.Threading.Channels;
using ErsatzTV.Core;
using ErsatzTV.Core.Domain;
using ErsatzTV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Channel = ErsatzTV.Core.Domain.Channel;

namespace ErsatzTV.Application.Channels;

public class UpdateChannelLogoHandler(
    ChannelWriter<IBackgroundServiceRequest> workerChannel,
    IDbContextFactory<TvContext> dbContextFactory)
    : IRequestHandler<UpdateChannelLogo, Either<BaseError, Unit>>
{
    public async Task<Either<BaseError, Unit>> Handle(
        UpdateChannelLogo request,
        CancellationToken cancellationToken)
    {
        await using TvContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        Channel channel = await dbContext.Channels
            .Include(c => c.Artwork)
            .FirstOrDefaultAsync(c => c.Id == request.ChannelId, cancellationToken);

        if (channel is null)
        {
            return BaseError.New("Channel does not exist");
        }

        {
            string path = request.ArtworkPath;
            if (path.StartsWith("iptv/logos/", StringComparison.Ordinal))
            {
                path = path.Replace("iptv/logos/", string.Empty);
            }

            channel.Artwork ??= [];

            Option<Artwork> maybeLogo = channel.Artwork
                .Where(a => a.ArtworkKind == ArtworkKind.Logo)
                .HeadOrNone();

            foreach (Artwork existing in maybeLogo)
            {
                existing.Path = path;
                existing.OriginalContentType = string.IsNullOrEmpty(request.ContentType) ? null : request.ContentType;
                existing.DateUpdated = DateTime.UtcNow;
            }

            if (maybeLogo.IsNone)
            {
                channel.Artwork.Add(
                    new Artwork
                    {
                        Path = path,
                        OriginalContentType = string.IsNullOrEmpty(request.ContentType)
                            ? null
                            : request.ContentType,
                        DateAdded = DateTime.UtcNow,
                        DateUpdated = DateTime.UtcNow,
                        ArtworkKind = ArtworkKind.Logo
                    });
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // The channel list caches logo urls, so it has to be told.
            await workerChannel.WriteAsync(new RefreshChannelList(), cancellationToken);

            return Unit.Default;
        }
    }
}
