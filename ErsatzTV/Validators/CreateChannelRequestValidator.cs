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

public class UpdateChannelRequestValidator : AbstractValidator<UpdateChannelRequest>
{
    public UpdateChannelRequestValidator()
    {
        // an existing channel has no "assign me a number" affordance, so both are required here
        RuleFor(x => x.Number).NotEmpty().Matches(Channel.NumberValidator)
            .WithMessage("Invalid channel number; two decimals are allowed for subchannels");
        RuleFor(x => x.FFmpegProfileId).NotNull().GreaterThan(0);

        Include(new CreateChannelRequestValidator());
    }
}
