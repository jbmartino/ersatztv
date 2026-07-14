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
    [EndpointGroupName("general")]
    [ProducesResponseType(typeof(ChannelViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateOne(
        int id,
        [Required] [FromBody]
        UpdateChannelRequest request)
    {
        var command = new UpdateChannel(
            id,
            request.Name,
            request.Number,
            request.Group,
            request.Categories ?? string.Empty,
            request.FFmpegProfileId ?? 0,
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

        Either<BaseError, ChannelViewModel> result = await mediator.Send(command);
        return result.Match<IActionResult>(Ok, error => Problem(error.ToString()));
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
