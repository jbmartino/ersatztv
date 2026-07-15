using ErsatzTV.Core.Api.Filler;

namespace ErsatzTV.Application.Filler;

public record GetAllFillerPresetsForApi : IRequest<List<FillerPresetResponseModel>>;
