using System.ComponentModel.DataAnnotations;
using ErsatzTV.Application.Libraries;
using ErsatzTV.Application.MediaItems;
using ErsatzTV.Application.Television;
using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Core;
using ErsatzTV.Core.Interfaces.Repositories;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ErsatzTV.Controllers.Api;

[ApiController]
[EndpointGroupName("general")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class LibrariesController(ITelevisionRepository televisionRepository, IMediator mediator) : ControllerBase
{
    [HttpGet("/api/libraries", Name = "GetLibraries")]
    [Tags("Libraries")]
    [EndpointSummary("Get all configured libraries")]
    [ProducesResponseType(typeof(List<LibraryViewModel>), StatusCodes.Status200OK)]
    public async Task<List<LibraryViewModel>> GetAll() => await mediator.Send(new GetConfiguredLibraries());

    [HttpPost("/api/libraries", Name = "CreateLocalLibrary")]
    [Tags("Libraries")]
    [EndpointSummary("Create a local library")]
    [ProducesResponseType(typeof(LocalLibraryViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddLocalLibrary(
        [Required] [FromBody]
        CreateLocalLibraryRequest request)
    {
        Either<BaseError, LocalLibraryViewModel> result = await mediator.Send(
            new CreateLocalLibrary(request.Name, request.MediaKind, request.Paths ?? []));

        return result.Match<IActionResult>(
            library => new OkObjectResult(library),
            error => Problem(error.ToString()));
    }

    /// <summary>
    ///     Television shows, with the ids needed to build a collection.
    /// </summary>
    [HttpGet("/api/media/shows", Name = "GetTelevisionShows")]
    [Tags("Libraries")]
    [EndpointSummary("Get all television shows")]
    [ProducesResponseType(typeof(List<NamedMediaItemViewModel>), StatusCodes.Status200OK)]
    public async Task<List<NamedMediaItemViewModel>> GetShows() => await mediator.Send(new GetAllTelevisionShows());

    [HttpPost("/api/libraries/{id:int}/scan")]
    [Tags("Libraries")]
    [EndpointSummary("Scan library")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScanLibrary(int id) =>
        await mediator.Send(new QueueLibraryScanByLibraryId(id))
            ? new OkResult()
            : new NotFoundResult();

    [HttpPost("/api/libraries/{id:int}/scan-show")]
    [Tags("Libraries")]
    [EndpointSummary("Scan show")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ScanShow(int id, [FromBody] ScanShowRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ShowTitle))
        {
            return new BadRequestObjectResult(new { error = "ShowTitle is required" });
        }

        string trimmedTitle = request.ShowTitle.Trim();
        Option<int> maybeShowId = await televisionRepository.GetShowIdByTitle(id, trimmedTitle);
        foreach (int showId in maybeShowId)
        {
            bool result = await mediator.Send(new QueueShowScanByLibraryId(id, showId, trimmedTitle, request.DeepScan));

            return result
                ? new OkResult()
                : new BadRequestObjectResult(new { error = "Unable to queue show scan. Library may not exist, may not support single show scanning, or may already be scanning." });
        }

        return new BadRequestObjectResult(
            new { error = $"Unable to locate show with title {request.ShowTitle} in library {id}" });
    }
}

public record ScanShowRequest(string ShowTitle, bool DeepScan = false);
