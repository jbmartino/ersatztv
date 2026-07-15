using ErsatzTV.Application.Filler;
using ErsatzTV.Core.Api.Filler;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ErsatzTV.Controllers.Api;

[ApiController]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class FillerController(IMediator mediator) : ControllerBase
{
    [HttpGet("/api/fillers", Name = "GetFillers")]
    [Tags("Fillers")]
    [EndpointSummary("List filler presets")]
    [EndpointDescription("Returns each filler preset's id and name so a client can reference one by name.")]
    [EndpointGroupName("general")]
    [ProducesResponseType(typeof(List<FillerPresetResponseModel>), StatusCodes.Status200OK)]
    public async Task<List<FillerPresetResponseModel>> GetAll(CancellationToken cancellationToken) =>
        await mediator.Send(new GetAllFillerPresetsForApi(), cancellationToken);
}
