using ErsatzTV.Core.Domain;

namespace ErsatzTV.Controllers.Api.Models;

/// <summary>
///     A create-channel request. Only Name is required; everything else mirrors the defaults the channel editor
///     applies to a new channel, so a minimal request is { "name": "...", "number": "4", "ffmpegProfileId": 1 }.
///     Number is assigned automatically (max existing + 1) when omitted, and FFmpegProfileId falls back to the
///     configured default profile.
/// </summary>
public record CreateChannelRequest
{
    public string Name { get; init; }
    public string Number { get; init; }
    public string Group { get; init; } = "ErsatzTV";
    public string Categories { get; init; } = string.Empty;
    public int? FFmpegProfileId { get; init; }
    public double? SlugSeconds { get; init; }
    public string LogoPath { get; init; }
    public string LogoContentType { get; init; }
    public ChannelStreamSelectorMode StreamSelectorMode { get; init; } = ChannelStreamSelectorMode.Default;
    public string StreamSelector { get; init; }
    public string PreferredAudioLanguageCode { get; init; }
    public string PreferredAudioTitle { get; init; }
    public ChannelPlayoutSource PlayoutSource { get; init; } = ChannelPlayoutSource.Generated;
    public ChannelPlayoutMode PlayoutMode { get; init; } = ChannelPlayoutMode.Continuous;
    public int? MirrorSourceChannelId { get; init; }
    public TimeSpan? PlayoutOffset { get; init; }
    public StreamingEngine StreamingEngine { get; init; } = StreamingEngine.Legacy;
    public NextEngineTextSubtitleMode NextEngineTextSubtitleMode { get; init; } = NextEngineTextSubtitleMode.Burn;
    public StreamingMode StreamingMode { get; init; } = StreamingMode.TransportStreamHybrid;
    public int? WatermarkId { get; init; }
    public int? FallbackFillerId { get; init; }
    public string PreferredSubtitleLanguageCode { get; init; }
    public ChannelSubtitleMode SubtitleMode { get; init; } = ChannelSubtitleMode.None;
    public ChannelMusicVideoCreditsMode MusicVideoCreditsMode { get; init; } = ChannelMusicVideoCreditsMode.None;
    public string MusicVideoCreditsTemplate { get; init; }
    public ChannelSongVideoMode SongVideoMode { get; init; } = ChannelSongVideoMode.Default;
    public ChannelTranscodeMode TranscodeMode { get; init; } = ChannelTranscodeMode.OnDemand;
    public ChannelIdleBehavior IdleBehavior { get; init; } = ChannelIdleBehavior.StopOnDisconnect;
    public bool IsEnabled { get; init; } = true;
    public bool ShowInEpg { get; init; } = true;
}

/// <summary>
///     An update-channel request. Same shape as create, but Number and FFmpegProfileId are required because there
///     is no sensible "leave it alone" default for an existing channel.
/// </summary>
public record UpdateChannelRequest : CreateChannelRequest;
