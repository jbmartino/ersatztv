using ErsatzTV.Core.Api.Filler;
using ErsatzTV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErsatzTV.Application.Filler;

public class GetAllFillerPresetsForApiHandler(IDbContextFactory<TvContext> dbContextFactory)
    : IRequestHandler<GetAllFillerPresetsForApi, List<FillerPresetResponseModel>>
{
    public async Task<List<FillerPresetResponseModel>> Handle(
        GetAllFillerPresetsForApi request,
        CancellationToken cancellationToken)
    {
        await using TvContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.FillerPresets
            .OrderBy(f => f.Name)
            .Select(f => new FillerPresetResponseModel(f.Id, f.Name))
            .ToListAsync(cancellationToken);
    }
}
