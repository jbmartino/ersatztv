using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Core.Domain;
using FluentValidation;

namespace ErsatzTV.Validators;

/// <summary>
///     The API equivalent of <see cref="ChannelEditViewModelValidator" />. Registered by
///     AddValidatorsFromAssemblyContaining&lt;Startup&gt;() and run automatically on model binding, so an API
///     caller cannot create a channel the channel editor would have rejected.
/// </summary>
public class CreateChannelRequestValidator : AbstractValidator<CreateChannelRequest>
{
    public CreateChannelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.Group).NotEmpty();

        // number is optional on create (auto-assigned), but must be valid when supplied
        When(
            x => !string.IsNullOrWhiteSpace(x.Number),
            () =>
            {
                RuleFor(x => x.Number).Matches(Channel.NumberValidator)
                    .WithMessage("Invalid channel number; two decimals are allowed for subchannels");
            });

        // likewise, the default profile is used when this is omitted
        When(
            x => x.FFmpegProfileId.HasValue,
            () => { RuleFor(x => x.FFmpegProfileId).GreaterThan(0); });

        When(
            x => !string.IsNullOrWhiteSpace(x.LogoPath) && Artwork.IsExternalUrl(x.LogoPath),
            () =>
            {
                RuleFor(x => x.LogoPath)
                    .Must(Artwork.IsExternalUrl)
                    .WithMessage("External logo url is invalid");
            });

        When(
            x => !x.IsEnabled,
            () =>
            {
                RuleFor(x => x.ShowInEpg)
                    .Must(x => !x)
                    .WithMessage("Disabled channels cannot be shown in EPG");
            });
    }
}

/// <summary>
///     Every field on an update is optional and an omitted one keeps the channel's current value, so each rule only
///     applies when the caller actually supplied the field. This deliberately does not include
///     <see cref="CreateChannelRequestValidator" />: create's rules assume defaults have been filled in, and they
///     would reject the partial body that an update is allowed to send.
/// </summary>
public class UpdateChannelRequestValidator : AbstractValidator<UpdateChannelRequest>
{
    public UpdateChannelRequestValidator()
    {
        // a channel always has these, so supplying one means supplying a real value; omit it to leave it alone
        When(x => x.Name is not null, () => { RuleFor(x => x.Name).NotEmpty(); });
        When(x => x.Group is not null, () => { RuleFor(x => x.Group).NotEmpty(); });

        When(
            x => x.Number is not null,
            () =>
            {
                RuleFor(x => x.Number).NotEmpty().Matches(Channel.NumberValidator)
                    .WithMessage("Invalid channel number; two decimals are allowed for subchannels");
            });

        When(x => x.FFmpegProfileId.HasValue, () => { RuleFor(x => x.FFmpegProfileId).GreaterThan(0); });

        When(
            x => !string.IsNullOrWhiteSpace(x.LogoPath) && Artwork.IsExternalUrl(x.LogoPath),
            () =>
            {
                RuleFor(x => x.LogoPath)
                    .Must(Artwork.IsExternalUrl)
                    .WithMessage("External logo url is invalid");
            });

        // only catchable when both are supplied; otherwise the update handler resolves it against the current channel
        When(
            x => x.IsEnabled is false && x.ShowInEpg is not null,
            () =>
            {
                RuleFor(x => x.ShowInEpg)
                    .Must(x => x is false)
                    .WithMessage("Disabled channels cannot be shown in EPG");
            });
    }
}
