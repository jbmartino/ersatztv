using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;
using ErsatzTV.Application;
using ErsatzTV.Application.Playouts;
using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Core;
using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Scheduling;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ErsatzTV.Controllers.Api;

[ApiController]
[EndpointGroupName("general")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class PlayoutController(ChannelWriter<IBackgroundServiceRequest> workerChannel, IMediator mediator)
    : ControllerBase
{
    [HttpGet("/api/playouts", Name = "GetPlayouts")]
    [Tags("Playouts")]
    [EndpointSummary("Get all playouts")]
    [ProducesResponseType(typeof(List<PlayoutResponseModel>), StatusCodes.Status200OK)]
    public async Task<List<PlayoutResponseModel>> GetAll()
    {
        // PageNum is zero-based: the handler does Skip(PageNum * PageSize), so passing 1 here
        // skipped every row and returned an empty list.
        PagedPlayoutsViewModel result = await mediator.Send(new GetPagedPlayouts(string.Empty, 0, int.MaxValue));
        return result.Page.Map(PlayoutResponseModel.From).ToList();
    }

    [HttpGet("/api/playouts/{id:int}", Name = "GetPlayout")]
    [Tags("Playouts")]
    [EndpointSummary("Get a playout, including its build status")]
    [ProducesResponseType(typeof(PlayoutResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOne(int id)
    {
        Option<PlayoutNameViewModel> maybePlayout = await mediator.Send(new GetPlayoutById(id));
        return maybePlayout.Match<IActionResult>(
            playout => Ok(PlayoutResponseModel.From(playout)),
            () => NotFound());
    }

    /// <summary>
    ///     Creates a classic playout, which requires a program schedule that already has at least one item.
    /// </summary>
    [HttpPost("/api/playouts/classic", Name = "CreateClassicPlayout")]
    [Tags("Playouts")]
    [EndpointSummary("Create a classic playout")]
    [ProducesResponseType(typeof(CreatePlayoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> AddClassic(
        [Required] [FromBody]
        CreateClassicPlayoutRequest request) =>
        Create(new CreateClassicPlayout(request.ChannelId, request.ProgramScheduleId));

    /// <summary>
    ///     Creates a sequential (YAML) playout. The schedule is read from disk on every build, so scheduling
    ///     can live in version control rather than in the database. Reference it by ScheduleName (uploaded
    ///     through /api/schedules) or by an absolute ScheduleFile path on the server.
    /// </summary>
    [HttpPost("/api/playouts/sequential", Name = "CreateSequentialPlayout")]
    [Tags("Playouts")]
    [EndpointSummary("Create a sequential (YAML) playout")]
    [ProducesResponseType(typeof(CreatePlayoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> AddSequential(
        [Required] [FromBody]
        CreateSequentialPlayoutRequest request)
    {
        bool hasName = !string.IsNullOrWhiteSpace(request.ScheduleName);
        bool hasFile = !string.IsNullOrWhiteSpace(request.ScheduleFile);

        if (hasName == hasFile)
        {
            ModelState.AddModelError(
                nameof(request.ScheduleName),
                "Exactly one of ScheduleName or ScheduleFile is required");
            return Task.FromResult<IActionResult>(ValidationProblem(ModelState));
        }

        string scheduleFile = request.ScheduleFile;
        if (hasName)
        {
            if (!SchedulesController.TryResolvePath(request.ScheduleName, out scheduleFile))
            {
                ModelState.AddModelError(nameof(request.ScheduleName), "Invalid schedule name");
                return Task.FromResult<IActionResult>(ValidationProblem(ModelState));
            }
        }

        return Create(new CreateSequentialPlayout(request.ChannelId, scheduleFile));
    }

    [HttpPost("/api/playouts/scripted", Name = "CreateScriptedPlayout")]
    [Tags("Playouts")]
    [EndpointSummary("Create a scripted playout")]
    [ProducesResponseType(typeof(CreatePlayoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> AddScripted(
        [Required] [FromBody]
        CreateFilePlayoutRequest request) =>
        Create(new CreateScriptedPlayout(request.ChannelId, request.ScheduleFile));

    [HttpPost("/api/playouts/external-json", Name = "CreateExternalJsonPlayout")]
    [Tags("Playouts")]
    [EndpointSummary("Create an external json playout")]
    [ProducesResponseType(typeof(CreatePlayoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> AddExternalJson(
        [Required] [FromBody]
        CreateFilePlayoutRequest request) =>
        Create(new CreateExternalJsonPlayout(request.ChannelId, request.ScheduleFile));

    [HttpPost("/api/playouts/block", Name = "CreateBlockPlayout")]
    [Tags("Playouts")]
    [EndpointSummary("Create a block playout")]
    [ProducesResponseType(typeof(CreatePlayoutResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<IActionResult> AddBlock(
        [Required] [FromBody]
        CreateBlockPlayoutRequest request) =>
        Create(new CreateBlockPlayout(request.ChannelId));

    [HttpDelete("/api/playouts/{id:int}", Name = "DeletePlayout")]
    [Tags("Playouts")]
    [EndpointSummary("Delete a playout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteOne(int id)
    {
        Either<BaseError, Unit> result = await mediator.Send(new DeletePlayout(id));
        return result.Match<IActionResult>(_ => Ok(), error => Problem(error.ToString()));
    }

    /// <summary>
    ///     Queues a playout build. This returns as soon as the build is queued, not when it finishes; poll
    ///     GET /api/playouts/{id} and check buildStatus to observe completion.
    /// </summary>
    [HttpPost("/api/playouts/{id:int}/build", Name = "BuildPlayout")]
    [Tags("Playouts")]
    [EndpointSummary("Queue a playout build")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Build(int id, [FromQuery] PlayoutBuildMode? mode)
    {
        Option<PlayoutNameViewModel> maybePlayout = await mediator.Send(new GetPlayoutById(id));
        foreach (PlayoutNameViewModel playout in maybePlayout)
        {
            // matches the reset behavior of the playouts page: classic playouts keep their collection progress,
            // every other kind is rebuilt from scratch
            PlayoutBuildMode buildMode = mode ?? playout.ScheduleKind switch
            {
                PlayoutScheduleKind.Classic => PlayoutBuildMode.Refresh,
                _ => PlayoutBuildMode.Reset
            };

            await workerChannel.WriteAsync(new BuildPlayout(id, buildMode));
            return Accepted();
        }

        return NotFound();
    }

    private async Task<IActionResult> Create(CreatePlayout command)
    {
        Either<BaseError, CreatePlayoutResponse> result = await mediator.Send(command);
        return result.Match<IActionResult>(
            playout => CreatedAtRoute("GetPlayout", new { id = playout.PlayoutId }, playout),
            error => Problem(error.ToString()));
    }
}
