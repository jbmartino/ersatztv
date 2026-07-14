using ErsatzTV.Application.Artworks;
using ErsatzTV.Application.Channels;
using ErsatzTV.Core.Domain;

namespace ErsatzTV.Controllers.Api.Models;

/// <summary>
///     Turns a partial update request into a full update command by filling in everything the caller left out from the
///     channel as it exists today.
///
///     The command layer is a full replacement, which is what the blazor channel editor needs: it always posts every
///     field. An api client does not, so without this a request that set one field would reset the other thirty to
///     their create-time defaults, silently changing streaming mode, transcode mode, idle behavior, the watermark and
///     the logo on a channel the caller only meant to rename.
/// </summary>
public static class ChannelUpdateMerge
{
    public static UpdateChannel MergeOnto(int id, UpdateChannelRequest request, ChannelViewModel existing)
    {
        ChannelMusicVideoCreditsMode musicVideoCreditsMode =
            request.MusicVideoCreditsMode ?? existing.MusicVideoCreditsMode;

        // a template is only meaningful when generating subtitles, which is the invariant the channel editor keeps
        string musicVideoCreditsTemplate = musicVideoCreditsMode is ChannelMusicVideoCreditsMode.GenerateSubtitles
            ? request.MusicVideoCreditsTemplate ?? existing.MusicVideoCreditsTemplate
            : null;

        // unlike create, the streaming mode is left as-is for the next engine; the update handler already coerces an
        // incompatible mode, and it accepts more modes than the create path forces
        return new UpdateChannel(
            id,
            request.Name ?? existing.Name,
            request.Number ?? existing.Number,
            request.Group ?? existing.Group,
            request.Categories ?? existing.Categories ?? string.Empty,
            request.FFmpegProfileId ?? existing.FFmpegProfileId,
            request.SlugSecondsSet ? request.SlugSeconds : existing.SlugSeconds,
            LogoFor(request, existing),
            request.StreamSelectorMode ?? existing.StreamSelectorMode,
            request.StreamSelector ?? existing.StreamSelector,
            request.PreferredAudioLanguageCode ?? existing.PreferredAudioLanguageCode,
            request.PreferredAudioTitle ?? existing.PreferredAudioTitle,
            request.PlayoutSource ?? existing.PlayoutSource,
            request.PlayoutMode ?? existing.PlayoutMode,
            request.MirrorSourceChannelIdSet ? request.MirrorSourceChannelId : existing.MirrorSourceChannelId,
            request.PlayoutOffsetSet ? request.PlayoutOffset : existing.PlayoutOffset,
            request.StreamingEngine ?? existing.StreamingEngine,
            request.NextEngineTextSubtitleMode ?? existing.NextEngineTextSubtitleMode,
            request.StreamingMode ?? existing.StreamingMode,
            request.WatermarkIdSet ? request.WatermarkId : existing.WatermarkId,
            request.FallbackFillerIdSet ? request.FallbackFillerId : existing.FallbackFillerId,
            request.PreferredSubtitleLanguageCode ?? existing.PreferredSubtitleLanguageCode,
            request.SubtitleMode ?? existing.SubtitleMode,
            musicVideoCreditsMode,
            musicVideoCreditsTemplate,
            request.SongVideoMode ?? existing.SongVideoMode,
            request.TranscodeMode ?? existing.TranscodeMode,
            request.IdleBehavior ?? existing.IdleBehavior,
            request.IsEnabled ?? existing.IsEnabled,
            request.ShowInEpg ?? existing.ShowInEpg);
    }

    /// <summary>
    ///     An omitted logo path carries the current logo forward. The view model already renders it as
    ///     "iptv/logos/{path}", which the update handler strips back off, so the value round-trips. An empty path
    ///     removes the logo, which is what the handler does with a blank path.
    /// </summary>
    private static ArtworkContentTypeModel LogoFor(UpdateChannelRequest request, ChannelViewModel existing) =>
        request.LogoPath is null
            ? existing.Logo ?? ArtworkContentTypeModel.None
            : new ArtworkContentTypeModel(request.LogoPath, request.LogoContentType ?? string.Empty);
}
