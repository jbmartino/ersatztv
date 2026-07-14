using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.Channels;
using ErsatzTV.Application;
using ErsatzTV.Application.Artworks;
using ErsatzTV.Application.Channels;
using ErsatzTV.Application.FFmpegProfiles;
using ErsatzTV.Application.Playouts;
using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Core;
using ErsatzTV.Core.Api.Channels;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Scheduling;
using ErsatzTV.Filters;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ErsatzTV.Controllers.Api;

[ApiController]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class ChannelController(ChannelWriter<IBackgroundServiceRequest> workerChannel, IMediator mediator)
    : ControllerBase
{
    [HttpGet("/api/channels")]
    [EndpointGroupName("general")]
    [ProducesResponseType(typeof(List<ChannelResponseModel>), StatusCodes.Status200OK)]
    public async Task<List<ChannelResponseModel>> GetAll() => await mediator.Send(new GetAllChannelsForApi());

    [HttpGet("/api/channels/{id:int}", Name = "GetChannel")]
    [Tags("Channels")]
    [EndpointSummary("Get a channel")]
    [EndpointGroupName("general")]
    [ProducesResponseType(typeof(ChannelViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOne(int id)
    {
        Option<ChannelViewModel> maybeChannel = await mediator.Send(new GetChannelById(id));
        return maybeChannel.Match<IActionResult>(Ok, () => NotFound());
    }

    [HttpPost("/api/channels", Name = "CreateChannel")]
    [Tags("Channels")]
    [EndpointSummary("Create a channel")]
    [EndpointGroupName("general")]
    [ProducesResponseType(typeof(CreateChannelResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddOne(
        [Required] [FromBody]
        CreateChannelRequest request)
    {
        string number = request.Number;
        if (string.IsNullOrWhiteSpace(number))
        {
            number = await NextChannelNumber();
        }

        int ffmpegProfileId = request.FFmpegProfileId ?? await DefaultFFmpegProfileId();
        if (ffmpegProfileId <= 0)
        {
            return Problem("No ffmpegProfileId was supplied and no default ffmpeg profile is configured");
        }

        var command = new CreateChannel(
            request.Name,
            number,
            request.Group,
            request.Categories ?? string.Empty,
            ffmpegProfileId,
            request.SlugSeconds,
            Logo(request),
            request.StreamSelectorMode,
            request.StreamSelector,
            request.PreferredAudioLanguageCode,
            request.PreferredAudioTitle,
            request.PlayoutSource,
            request.PlayoutMode,
            request.MirrorSourceChannelId,
            request.PlayoutOffset,
            request.StreamingEngine,
            request.NextEngineTextSubtitleMode,
            StreamingModeFor(request),
            request.WatermarkId,
            request.FallbackFillerId,
            request.PreferredSubtitleLanguageCode,
            request.SubtitleMode,
            request.MusicVideoCreditsMode,
            MusicVideoCreditsTemplateFor(request),
            request.SongVideoMode,
            request.TranscodeMode,
            request.IdleBehavior,
            request.IsEnabled,
            request.ShowInEpg);

        Either<BaseError, CreateChannelResult> result = await mediator.Send(command);
        return result.Match<IActionResult>(
            channel => CreatedAtRoute("GetChannel", new { id = channel.ChannelId }, channel),
            error => Problem(error.ToString()));
    }

    [HttpPut("/api/channels/{id:int}", Name = "UpdateChannel")]
    [Tags("Channels")]
    [EndpointSummary("Update a channel")]
    [EndpointDescription(
        "Fields that are omitted keep their current value, so a partial body is safe. SlugSeconds, "
        + "MirrorSourceChannelId, PlayoutOffset, WatermarkId and FallbackFillerId are cleared by sending an "
        + "explicit null; a string is cleared by sending an empty string.")]
    [EndpointGroupName("general")]
    [ProducesResponseType(typeof(ChannelViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateOne(
        int id,
        [Required] [FromBody]
        UpdateChannelRequest request)
    {
        Option<ChannelViewModel> maybeChannel = await mediator.Send(new GetChannelById(id));
        foreach (ChannelViewModel existing in maybeChannel)
        {
            Either<BaseError, ChannelViewModel> result = await mediator.Send(MergeOnto(id, request, existing));
            return result.Match<IActionResult>(Ok, error => Problem(error.ToString()));
        }

        return NotFound();
    }

    [HttpDelete("/api/channels/{id:int}", Name = "DeleteChannel")]
    [Tags("Channels")]
    [EndpointSummary("Delete a channel")]
    [EndpointGroupName("general")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteOne(int id)
    {
        Either<BaseError, Unit> result = await mediator.Send(new DeleteChannel(id));
        return result.Match<IActionResult>(_ => Ok(), error => Problem(error.ToString()));
    }

    [HttpPost("/api/channels/{channelNumber}/playout/reset")]
    [Tags("Channels")]
    [EndpointSummary("Reset channel playout")]
    [EndpointGroupName("general")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPlayout(string channelNumber)
    {
        Option<int> maybePlayoutId = await mediator.Send(new GetPlayoutIdByChannelNumber(channelNumber));
        foreach (int playoutId in maybePlayoutId)
        {
            await workerChannel.WriteAsync(new BuildPlayout(playoutId, PlayoutBuildMode.Reset));
            return Ok();
        }

        return NotFound();
    }

    /// <summary>
    ///     Builds an update command from the channel as it exists today, overwriting only what the request actually
    ///     supplied. Anything the caller omitted round-trips its current value, so a partial body cannot silently
    ///     reset the fields it did not mention.
    /// </summary>
    private static UpdateChannel MergeOnto(int id, UpdateChannelRequest request, ChannelViewModel existing)
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

    private static ArtworkContentTypeModel Logo(CreateChannelRequest request) =>
        string.IsNullOrWhiteSpace(request.LogoPath)
            ? ArtworkContentTypeModel.None
            : new ArtworkContentTypeModel(request.LogoPath, request.LogoContentType ?? string.Empty);

    // the next engine only supports the hls segmenter; the channel editor forces this on the client side
    private static StreamingMode StreamingModeFor(CreateChannelRequest request) =>
        request.StreamingEngine is StreamingEngine.Next
            ? StreamingMode.HttpLiveStreamingSegmenter
            : request.StreamingMode;

    private static string MusicVideoCreditsTemplateFor(CreateChannelRequest request) =>
        request.MusicVideoCreditsMode is ChannelMusicVideoCreditsMode.GenerateSubtitles
            ? request.MusicVideoCreditsTemplate
            : null;

    /// <summary>
    ///     Mirrors the "new channel" defaults in ChannelEditor.razor: the next unused whole channel number.
    /// </summary>
    private async Task<string> NextChannelNumber()
    {
        List<ChannelViewModel> channels = await mediator.Send(new GetAllChannels());
        int maxNumber = channels
            .Map(c => int.TryParse(c.Number.Split(".").Head(), out int result) ? result : 0)
            .DefaultIfEmpty(0)
            .Max();

        return (maxNumber + 1).ToString(CultureInfo.InvariantCulture);
    }

    private async Task<int> DefaultFFmpegProfileId()
    {
        FFmpegSettingsViewModel settings = await mediator.Send(new GetFFmpegSettings());
        return settings.DefaultFFmpegProfileId;
    }
}
