using ErsatzTV.Core;

namespace ErsatzTV.Application.Channels;

/// <summary>
///     Replaces only a channel's logo.
///
///     UpdateChannel is a full replacement, and GET /api/channels does not return every updatable
///     field, so an API client cannot safely read-modify-write a channel just to change its logo:
///     doing so would reset streaming mode, transcode mode and the rest to their defaults. This
///     command exists so setting a logo cannot have side effects.
/// </summary>
public record UpdateChannelLogo(int ChannelId, string ArtworkPath, string ContentType)
    : IRequest<Either<BaseError, Unit>>;
