using ErsatzTV.Application.Artworks;
using ErsatzTV.Application.Channels;
using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Core.Domain;
using NUnit.Framework;
using Shouldly;

namespace ErsatzTV.Tests.Controllers;

/// <summary>
///     PUT /api/channels used to be a full replacement while GET returned only a handful of fields, so read-modify-write
///     was impossible and any client that sent a partial body silently reset streaming mode, transcode mode, idle
///     behavior, the watermark, the filler and the logo to their create-time defaults. These tests pin the merge that
///     replaced it: what the caller omits keeps its current value.
/// </summary>
[TestFixture]
public class ChannelUpdateMergeTests
{
    private static ChannelViewModel Existing() =>
        new(
            1,
            "2",
            "Comedy Central",
            "Group",
            "categories",
            7,
            12.5,
            new ArtworkContentTypeModel("iptv/logos/ABC123", "image/png"),
            ChannelStreamSelectorMode.Default,
            "selector",
            "eng",
            "audio title",
            ChannelPlayoutSource.Generated,
            ChannelPlayoutMode.Continuous,
            null,
            null,
            StreamingEngine.Legacy,
            NextEngineTextSubtitleMode.Burn,
            StreamingMode.TransportStream,
            42,
            43,
            0,
            "spa",
            ChannelSubtitleMode.Any,
            ChannelMusicVideoCreditsMode.None,
            null,
            ChannelSongVideoMode.Default,
            ChannelTranscodeMode.OnDemand,
            ChannelIdleBehavior.KeepRunning,
            true,
            true);

    [Test]
    public void Empty_Request_Should_Change_Nothing()
    {
        UpdateChannel result = ChannelUpdateMerge.MergeOnto(1, new UpdateChannelRequest(), Existing());

        result.Name.ShouldBe("Comedy Central");
        result.Number.ShouldBe("2");
        result.Group.ShouldBe("Group");
        result.Categories.ShouldBe("categories");
        result.FFmpegProfileId.ShouldBe(7);
        result.SlugSeconds.ShouldBe(12.5);
        result.StreamSelectorMode.ShouldBe(ChannelStreamSelectorMode.Default);
        result.StreamSelector.ShouldBe("selector");
        result.PreferredAudioLanguageCode.ShouldBe("eng");
        result.PreferredAudioTitle.ShouldBe("audio title");
        result.PlayoutSource.ShouldBe(ChannelPlayoutSource.Generated);
        result.PlayoutMode.ShouldBe(ChannelPlayoutMode.Continuous);
        result.StreamingEngine.ShouldBe(StreamingEngine.Legacy);
        result.NextEngineTextSubtitleMode.ShouldBe(NextEngineTextSubtitleMode.Burn);
        result.StreamingMode.ShouldBe(StreamingMode.TransportStream);
        result.WatermarkId.ShouldBe(42);
        result.FallbackFillerId.ShouldBe(43);
        result.PreferredSubtitleLanguageCode.ShouldBe("spa");
        result.SubtitleMode.ShouldBe(ChannelSubtitleMode.Any);
        result.SongVideoMode.ShouldBe(ChannelSongVideoMode.Default);
        result.TranscodeMode.ShouldBe(ChannelTranscodeMode.OnDemand);
        result.IdleBehavior.ShouldBe(ChannelIdleBehavior.KeepRunning);
        result.IsEnabled.ShouldBeTrue();
        result.ShowInEpg.ShouldBeTrue();
    }

    /// <summary>
    ///     The real bug: renaming a channel must not take it off MPEG-TS, and must not delete its logo.
    /// </summary>
    [Test]
    public void Renaming_Should_Not_Touch_Streaming_Mode_Or_Logo()
    {
        var request = new UpdateChannelRequest { Name = "G4" };

        UpdateChannel result = ChannelUpdateMerge.MergeOnto(1, request, Existing());

        result.Name.ShouldBe("G4");
        result.StreamingMode.ShouldBe(StreamingMode.TransportStream);
        result.TranscodeMode.ShouldBe(ChannelTranscodeMode.OnDemand);
        result.IdleBehavior.ShouldBe(ChannelIdleBehavior.KeepRunning);
        result.Logo.Path.ShouldBe("iptv/logos/ABC123");
        result.Logo.ContentType.ShouldBe("image/png");
    }

    [Test]
    public void Supplied_Fields_Should_Be_Applied()
    {
        var request = new UpdateChannelRequest
        {
            Group = "RetroTV",
            StreamingMode = StreamingMode.HttpLiveStreamingSegmenter,
            IdleBehavior = ChannelIdleBehavior.StopOnDisconnect,
            IsEnabled = false
        };

        UpdateChannel result = ChannelUpdateMerge.MergeOnto(1, request, Existing());

        result.Group.ShouldBe("RetroTV");
        result.StreamingMode.ShouldBe(StreamingMode.HttpLiveStreamingSegmenter);
        result.IdleBehavior.ShouldBe(ChannelIdleBehavior.StopOnDisconnect);
        result.IsEnabled.ShouldBeFalse();

        // and nothing else moved
        result.Name.ShouldBe("Comedy Central");
        result.WatermarkId.ShouldBe(42);
    }

    /// <summary>
    ///     The five fields that are nullable on a channel need three states, otherwise there is no way to remove a
    ///     watermark or a fallback filler over the api at all.
    /// </summary>
    [Test]
    public void Omitted_Nullable_Fields_Should_Be_Left_Alone()
    {
        UpdateChannel result = ChannelUpdateMerge.MergeOnto(1, new UpdateChannelRequest(), Existing());

        result.WatermarkId.ShouldBe(42);
        result.FallbackFillerId.ShouldBe(43);
        result.SlugSeconds.ShouldBe(12.5);
    }

    [Test]
    public void Explicit_Null_Should_Clear_A_Nullable_Field()
    {
        var request = new UpdateChannelRequest { WatermarkId = null, FallbackFillerId = null, SlugSeconds = null };

        UpdateChannel result = ChannelUpdateMerge.MergeOnto(1, request, Existing());

        result.WatermarkId.ShouldBeNull();
        result.FallbackFillerId.ShouldBeNull();
        result.SlugSeconds.ShouldBeNull();
    }

    [Test]
    public void Value_Should_Set_A_Nullable_Field()
    {
        var request = new UpdateChannelRequest { WatermarkId = 99 };

        UpdateChannel result = ChannelUpdateMerge.MergeOnto(1, request, Existing());

        result.WatermarkId.ShouldBe(99);
        result.FallbackFillerId.ShouldBe(43);
    }

    [Test]
    public void Empty_Logo_Path_Should_Remove_The_Logo()
    {
        // a blank path is what the update handler treats as "delete the logo"
        var request = new UpdateChannelRequest { LogoPath = "" };

        UpdateChannel result = ChannelUpdateMerge.MergeOnto(1, request, Existing());

        result.Logo.Path.ShouldBe("");
    }

    [Test]
    public void Music_Video_Credits_Template_Should_Only_Survive_When_Generating_Subtitles()
    {
        ChannelViewModel existing = Existing() with
        {
            MusicVideoCreditsMode = ChannelMusicVideoCreditsMode.GenerateSubtitles,
            MusicVideoCreditsTemplate = "template"
        };

        ChannelUpdateMerge.MergeOnto(1, new UpdateChannelRequest(), existing)
            .MusicVideoCreditsTemplate.ShouldBe("template");

        var turnOff = new UpdateChannelRequest { MusicVideoCreditsMode = ChannelMusicVideoCreditsMode.None };
        ChannelUpdateMerge.MergeOnto(1, turnOff, existing).MusicVideoCreditsTemplate.ShouldBeNull();
    }

    /// <summary>
    ///     A channel with no logo has an empty artwork model rather than null, and the merge must not turn that into a
    ///     null the handler would dereference.
    /// </summary>
    [Test]
    public void Channel_With_No_Logo_Should_Merge_To_None()
    {
        ChannelViewModel existing = Existing() with { Logo = ArtworkContentTypeModel.None };

        UpdateChannel result = ChannelUpdateMerge.MergeOnto(1, new UpdateChannelRequest(), existing);

        result.Logo.ShouldNotBeNull();
        result.Logo.Path.ShouldBe("");
    }
}
