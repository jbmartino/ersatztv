using System.ComponentModel.DataAnnotations;
using ErsatzTV.Application.MediaCollections;
using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Core;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ErsatzTV.Controllers.Api;

[ApiController]
[EndpointGroupName("general")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class CollectionController(IMediator mediator) : ControllerBase
{
    [HttpGet("/api/collections", Name = "GetCollections")]
    [Tags("Collections")]
    [EndpointSummary("Get all collections")]
    [ProducesResponseType(typeof(List<CollectionResponseModel>), StatusCodes.Status200OK)]
    public async Task<List<CollectionResponseModel>> GetAll() =>
        await mediator.Send(new GetAllCollections())
            .Map(list => list.Map(CollectionResponseModel.From).ToList());

    [HttpGet("/api/collections/{id:int}", Name = "GetCollection")]
    [Tags("Collections")]
    [EndpointSummary("Get a collection")]
    [ProducesResponseType(typeof(CollectionResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOne(int id)
    {
        Option<MediaCollectionViewModel> maybeCollection = await mediator.Send(new GetCollectionById(id));
        return maybeCollection.Match<IActionResult>(
            collection => Ok(CollectionResponseModel.From(collection)),
            () => NotFound());
    }

    [HttpPost("/api/collections", Name = "CreateCollection")]
    [Tags("Collections")]
    [EndpointSummary("Create a collection")]
    [ProducesResponseType(typeof(CollectionResponseModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddOne(
        [Required] [FromBody]
        CreateCollectionRequest request)
    {
        Either<BaseError, MediaCollectionViewModel> result =
            await mediator.Send(new CreateCollection(request.Name));

        return result.Match<IActionResult>(
            collection => CreatedAtRoute(
                "GetCollection",
                new { id = collection.Id },
                CollectionResponseModel.From(collection)),
            error => Problem(error.ToString()));
    }

    [HttpPut("/api/collections/{id:int}", Name = "UpdateCollection")]
    [Tags("Collections")]
    [EndpointSummary("Update a collection")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateOne(
        int id,
        [Required] [FromBody]
        UpdateCollectionRequest request)
    {
        var command = new UpdateCollection(id, request.Name);
        if (request.UseCustomPlaybackOrder.HasValue)
        {
            command.UseCustomPlaybackOrder = request.UseCustomPlaybackOrder.Value;
        }

        Either<BaseError, Unit> result = await mediator.Send(command);
        return result.Match<IActionResult>(_ => Ok(), error => Problem(error.ToString()));
    }

    [HttpDelete("/api/collections/{id:int}", Name = "DeleteCollection")]
    [Tags("Collections")]
    [EndpointSummary("Delete a collection")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteOne(int id)
    {
        Either<BaseError, Unit> result = await mediator.Send(new DeleteCollection(id));
        return result.Match<IActionResult>(_ => Ok(), error => Problem(error.ToString()));
    }

    /// <summary>
    ///     Adds media items to a collection. Every affected playout is refreshed automatically.
    /// </summary>
    [HttpPost("/api/collections/{id:int}/items", Name = "AddCollectionItems")]
    [Tags("Collections")]
    [EndpointSummary("Add items to a collection")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddItems(
        int id,
        [Required] [FromBody]
        CollectionItemsRequest request)
    {
        var command = new AddItemsToCollection(
            id,
            request.MovieIds ?? [],
            request.ShowIds ?? [],
            request.SeasonIds ?? [],
            request.EpisodeIds ?? [],
            request.ArtistIds ?? [],
            request.MusicVideoIds ?? [],
            request.OtherVideoIds ?? [],
            request.SongIds ?? [],
            request.ImageIds ?? [],
            request.RemoteStreamIds ?? []);

        Either<BaseError, Unit> result = await mediator.Send(command);
        return result.Match<IActionResult>(_ => Ok(), error => Problem(error.ToString()));
    }

    [HttpDelete("/api/collections/{id:int}/items", Name = "RemoveCollectionItems")]
    [Tags("Collections")]
    [EndpointSummary("Remove items from a collection")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RemoveItems(
        int id,
        [Required] [FromBody]
        RemoveCollectionItemsRequest request)
    {
        var command = new RemoveItemsFromCollection(id) { MediaItemIds = request.MediaItemIds ?? [] };

        Either<BaseError, Unit> result = await mediator.Send(command);
        return result.Match<IActionResult>(_ => Ok(), error => Problem(error.ToString()));
    }
}
