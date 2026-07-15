using ErsatzTV.Core.Api.Watermarks;
using ErsatzTV.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ErsatzTV.Application.Watermarks;

public class GetAllWatermarksForApiHandler(IDbContextFactory<TvContext> dbContextFactory)
    : IRequestHandler<GetAllWatermarksForApi, List<WatermarkResponseModel>>
{
    public async Task<List<WatermarkResponseModel>> Handle(
        GetAllWatermarksForApi request,
        CancellationToken cancellationToken)
    {
        await using TvContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ChannelWatermarks
            .OrderBy(w => w.Name)
            .Select(w => new WatermarkResponseModel(w.Id, w.Name))
            .ToListAsync(cancellationToken);
    }
}
