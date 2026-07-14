using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Core.Domain;
using ErsatzTV.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;
using Shouldly;

namespace ErsatzTV.Tests.Controllers;

/// <summary>
///     Binds request bodies with the serializer settings the running server uses, because the update contract lives or
///     dies on telling an omitted field from an explicit null.
///
///     Two things make this work, and both are easy to break by accident. Newtonsoft only invokes a property setter for
///     a key that is actually present in the body, so the init accessor doubles as the presence flag. And the api is
///     configured with NullValueHandling.Ignore globally, which would drop an explicit null before it ever reached that
///     setter, so the nullable fields opt back in with NullValueHandling.Include.
/// </summary>
[TestFixture]
public class UpdateChannelRequestBindingTests
{
    private static readonly JsonSerializerSettings Settings = ApiJsonSettings.Create();

    private static UpdateChannelRequest Bind(string json) =>
        JsonConvert.DeserializeObject<UpdateChannelRequest>(json, Settings);

    [Test]
    public void Omitted_Field_Should_Not_Be_Set()
    {
        UpdateChannelRequest request = Bind("""{ "name": "G4" }""");

        request.Name.ShouldBe("G4");

        request.WatermarkIdSet.ShouldBeFalse();
        request.FallbackFillerIdSet.ShouldBeFalse();
        request.SlugSecondsSet.ShouldBeFalse();
        request.PlayoutOffsetSet.ShouldBeFalse();
        request.MirrorSourceChannelIdSet.ShouldBeFalse();
    }

    [Test]
    public void Explicit_Null_Should_Be_Set_And_Null()
    {
        UpdateChannelRequest request = Bind("""{ "watermarkId": null, "fallbackFillerId": null }""");

        request.WatermarkIdSet.ShouldBeTrue();
        request.WatermarkId.ShouldBeNull();

        request.FallbackFillerIdSet.ShouldBeTrue();
        request.FallbackFillerId.ShouldBeNull();

        // untouched fields stay unset
        request.SlugSecondsSet.ShouldBeFalse();
    }

    [Test]
    public void Value_Should_Be_Set_And_Carried()
    {
        UpdateChannelRequest request = Bind("""{ "watermarkId": 3, "slugSeconds": 7.5 }""");

        request.WatermarkIdSet.ShouldBeTrue();
        request.WatermarkId.ShouldBe(3);

        request.SlugSecondsSet.ShouldBeTrue();
        request.SlugSeconds.ShouldBe(7.5);
    }

    [Test]
    public void Empty_Body_Should_Set_Nothing()
    {
        UpdateChannelRequest request = Bind("{}");

        request.Name.ShouldBeNull();
        request.StreamingMode.ShouldBeNull();
        request.IsEnabled.ShouldBeNull();
        request.WatermarkIdSet.ShouldBeFalse();
        request.PlayoutOffsetSet.ShouldBeFalse();
    }

    [Test]
    public void Enums_Should_Bind_By_Name()
    {
        UpdateChannelRequest request = Bind(
            """{ "streamingMode": "TransportStreamHybrid", "transcodeMode": "OnDemand" }""");

        request.StreamingMode.ShouldBe(StreamingMode.TransportStreamHybrid);
        request.TranscodeMode.ShouldBe(ChannelTranscodeMode.OnDemand);
        request.IdleBehavior.ShouldBeNull();
    }

    /// <summary>
    ///     The presence flags are internal bookkeeping and must never appear in a response or in the openapi schema.
    /// </summary>
    [Test]
    public void Presence_Flags_Should_Not_Serialize()
    {
        string json = JsonConvert.SerializeObject(new UpdateChannelRequest { Name = "G4" }, Settings);

        // named individually rather than searching for "Set": shouldly compares case insensitively, and
        // "playoutOffset" contains one
        json.ShouldNotContain("slugSecondsSet");
        json.ShouldNotContain("mirrorSourceChannelIdSet");
        json.ShouldNotContain("playoutOffsetSet");
        json.ShouldNotContain("watermarkIdSet");
        json.ShouldNotContain("fallbackFillerIdSet");
        json.ShouldContain("G4");
    }
}
