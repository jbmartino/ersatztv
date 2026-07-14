using ErsatzTV.Core.Domain;
using Newtonsoft.Json;

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
///     An update-channel request. Every field is optional and omitting one leaves the channel's current value alone,
///     so a client can safely PUT a partial channel without knowing about fields it does not care about. This is
///     deliberately NOT a full replacement: an update request that defaulted its unset fields would silently reset
///     streaming mode, transcode mode, idle behavior, watermark and filler on any client that sent a partial body.
///
///     Most fields cannot be null on a channel, so null and omitted mean the same thing: leave it alone. To clear a
///     string, send "" rather than null. The five fields that ARE nullable on a channel (SlugSeconds,
///     MirrorSourceChannelId, PlayoutOffset, WatermarkId, FallbackFillerId) track presence separately, so they
///     distinguish "omitted" (leave alone) from an explicit null (clear it) -- otherwise there would be no way to
///     remove a watermark or a fallback filler over the API.
/// </summary>
public record UpdateChannelRequest
{
    private readonly double? _slugSeconds;
    private readonly int? _mirrorSourceChannelId;
    private readonly TimeSpan? _playoutOffset;
    private readonly int? _watermarkId;
    private readonly int? _fallbackFillerId;

    public string Name { get; init; }
    public string Number { get; init; }
    public string Group { get; init; }
    public string Categories { get; init; }
    public int? FFmpegProfileId { get; init; }

    /// <summary>Send "" to remove the channel's logo.</summary>
    public string LogoPath { get; init; }

    public string LogoContentType { get; init; }
    public ChannelStreamSelectorMode? StreamSelectorMode { get; init; }
    public string StreamSelector { get; init; }
    public string PreferredAudioLanguageCode { get; init; }
    public string PreferredAudioTitle { get; init; }
    public ChannelPlayoutSource? PlayoutSource { get; init; }
    public ChannelPlayoutMode? PlayoutMode { get; init; }
    public StreamingEngine? StreamingEngine { get; init; }
    public NextEngineTextSubtitleMode? NextEngineTextSubtitleMode { get; init; }
    public StreamingMode? StreamingMode { get; init; }
    public string PreferredSubtitleLanguageCode { get; init; }
    public ChannelSubtitleMode? SubtitleMode { get; init; }
    public ChannelMusicVideoCreditsMode? MusicVideoCreditsMode { get; init; }
    public string MusicVideoCreditsTemplate { get; init; }
    public ChannelSongVideoMode? SongVideoMode { get; init; }
    public ChannelTranscodeMode? TranscodeMode { get; init; }
    public ChannelIdleBehavior? IdleBehavior { get; init; }
    public bool? IsEnabled { get; init; }
    public bool? ShowInEpg { get; init; }

    // The nullable-on-the-channel fields. Newtonsoft only invokes a setter for a key that is actually present in the
    // body, so the init accessor doubles as the presence flag; NullValueHandling.Include is required because the
    // serializer is configured to ignore nulls globally, which would otherwise drop an explicit null before it lands.

    /// <summary>Omit to leave unchanged; send null to clear.</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public double? SlugSeconds
    {
        get => _slugSeconds;
        init
        {
            _slugSeconds = value;
            SlugSecondsSet = true;
        }
    }

    /// <summary>Omit to leave unchanged; send null to clear.</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public int? MirrorSourceChannelId
    {
        get => _mirrorSourceChannelId;
        init
        {
            _mirrorSourceChannelId = value;
            MirrorSourceChannelIdSet = true;
        }
    }

    /// <summary>Omit to leave unchanged; send null to clear.</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public TimeSpan? PlayoutOffset
    {
        get => _playoutOffset;
        init
        {
            _playoutOffset = value;
            PlayoutOffsetSet = true;
        }
    }

    /// <summary>Omit to leave unchanged; send null to remove the watermark.</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public int? WatermarkId
    {
        get => _watermarkId;
        init
        {
            _watermarkId = value;
            WatermarkIdSet = true;
        }
    }

    /// <summary>Omit to leave unchanged; send null to remove the fallback filler.</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public int? FallbackFillerId
    {
        get => _fallbackFillerId;
        init
        {
            _fallbackFillerId = value;
            FallbackFillerIdSet = true;
        }
    }

    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool SlugSecondsSet { get; private init; }

    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool MirrorSourceChannelIdSet { get; private init; }

    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool PlayoutOffsetSet { get; private init; }

    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool WatermarkIdSet { get; private init; }

    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public bool FallbackFillerIdSet { get; private init; }
}
