using ErsatzTV.Application.Watermarks;
using ErsatzTV.Core.Api.Watermarks;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ErsatzTV.Controllers.Api;

[ApiController]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class WatermarkController(IMediator mediator) : ControllerBase
{
    [HttpGet("/api/watermarks", Name = "GetWatermarks")]
    [Tags("Watermarks")]
    [EndpointSummary("List watermarks")]
    [EndpointDescription("Returns each watermark's id and name so a client can reference one by name.")]
    [EndpointGroupName("general")]
    [ProducesResponseType(typeof(List<WatermarkResponseModel>), StatusCodes.Status200OK)]
    public async Task<List<WatermarkResponseModel>> GetAll(CancellationToken cancellationToken) =>
        await mediator.Send(new GetAllWatermarksForApi(), cancellationToken);
}
