using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Validators;
using FluentValidation.Results;
using NUnit.Framework;
using Shouldly;

namespace ErsatzTV.Tests.Validators;

/// <summary>
///     These rules previously lived only in the blazor channel editor, so the api could create channels the ui
///     would have rejected.
/// </summary>
[TestFixture]
public class CreateChannelRequestValidatorTests
{
    private readonly CreateChannelRequestValidator _validator = new();

    [Test]
    public void Should_Accept_Minimal_Request()
    {
        // number and ffmpeg profile are filled in by the controller when omitted
        ValidationResult result = _validator.Validate(new CreateChannelRequest { Name = "Nickelodeon" });

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    public void Should_Require_Name()
    {
        ValidationResult result = _validator.Validate(new CreateChannelRequest { Name = "" });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Name");
    }

    [Test]
    [TestCase("4")]
    [TestCase("4.1")]
    [TestCase("104.20")]
    public void Should_Accept_Valid_Channel_Numbers(string number)
    {
        ValidationResult result = _validator.Validate(
            new CreateChannelRequest { Name = "Nickelodeon", Number = number });

        result.IsValid.ShouldBeTrue();
    }

    [Test]
    [TestCase("abc")]
    [TestCase("4.123")]
    [TestCase("-1")]
    [TestCase("4.")]
    public void Should_Reject_Invalid_Channel_Numbers(string number)
    {
        ValidationResult result = _validator.Validate(
            new CreateChannelRequest { Name = "Nickelodeon", Number = number });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Number");
    }

    [Test]
    public void Should_Reject_Disabled_Channel_Shown_In_Epg()
    {
        ValidationResult result = _validator.Validate(
            new CreateChannelRequest { Name = "Nickelodeon", IsEnabled = false, ShowInEpg = true });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == "Disabled channels cannot be shown in EPG");
    }

    [Test]
    public void Should_Reject_Non_Positive_FFmpeg_Profile_Id()
    {
        ValidationResult result = _validator.Validate(
            new CreateChannelRequest { Name = "Nickelodeon", FFmpegProfileId = 0 });

        result.IsValid.ShouldBeFalse();
    }
}

[TestFixture]
public class UpdateChannelRequestValidatorTests
{
    private readonly UpdateChannelRequestValidator _validator = new();

    [Test]
    public void Should_Require_Number_And_FFmpeg_Profile()
    {
        // unlike create, there is nothing to default to when updating an existing channel
        ValidationResult result = _validator.Validate(new UpdateChannelRequest { Name = "Nickelodeon" });

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Number");
        result.Errors.ShouldContain(e => e.PropertyName == "FFmpegProfileId");
    }

    [Test]
    public void Should_Accept_Complete_Request()
    {
        ValidationResult result = _validator.Validate(
            new UpdateChannelRequest { Name = "Nickelodeon", Number = "4", FFmpegProfileId = 1 });

        result.IsValid.ShouldBeTrue();
    }
}
