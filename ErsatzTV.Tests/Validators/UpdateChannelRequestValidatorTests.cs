using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Validators;
using FluentValidation.Results;
using NUnit.Framework;
using Shouldly;

namespace ErsatzTV.Tests.Validators;

/// <summary>
///     An update is a partial body, so every rule only applies when the caller actually supplied the field. The
///     validator used to include the create rules, which required a name and a number, so it rejected exactly the
///     partial updates the api is supposed to accept.
/// </summary>
[TestFixture]
public class UpdateChannelRequestValidatorTests
{
    private readonly UpdateChannelRequestValidator _validator = new();

    [Test]
    public void Should_Accept_An_Empty_Request()
    {
        ValidationResult result = _validator.Validate(new UpdateChannelRequest());

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public void Should_Accept_A_Partial_Request()
    {
        ValidationResult result = _validator.Validate(new UpdateChannelRequest { Name = "G4" });

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public void Should_Reject_An_Empty_Name_When_Supplied()
    {
        // omitting the name leaves it alone; sending "" is asking for a nameless channel
        ValidationResult result = _validator.Validate(new UpdateChannelRequest { Name = "" });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Test]
    public void Should_Reject_An_Invalid_Number_When_Supplied()
    {
        ValidationResult result = _validator.Validate(new UpdateChannelRequest { Number = "not-a-number" });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Number");
    }

    [Test]
    public void Should_Accept_A_Subchannel_Number()
    {
        ValidationResult result = _validator.Validate(new UpdateChannelRequest { Number = "2.1" });

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public void Should_Reject_A_Zero_FFmpeg_Profile_When_Supplied()
    {
        ValidationResult result = _validator.Validate(new UpdateChannelRequest { FFmpegProfileId = 0 });

        result.IsValid.ShouldBeFalse();
    }

    [Test]
    public void Should_Reject_A_Disabled_Channel_Shown_In_Epg()
    {
        ValidationResult result = _validator.Validate(
            new UpdateChannelRequest { IsEnabled = false, ShowInEpg = true });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ShowInEpg");
    }

    [Test]
    public void Should_Allow_Disabling_Without_Mentioning_Epg()
    {
        // the update handler resolves ShowInEpg against the channel, so this is not a contradiction the validator
        // can see, and it must not reject it
        ValidationResult result = _validator.Validate(new UpdateChannelRequest { IsEnabled = false });

        result.IsValid.ShouldBeTrue();
    }
}
