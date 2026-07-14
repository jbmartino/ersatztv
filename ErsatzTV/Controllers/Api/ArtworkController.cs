using ErsatzTV.Application.Channels;
using ErsatzTV.Application.Images;
using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Core;
using ErsatzTV.Core.Domain;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ErsatzTV.Controllers.Api;

/// <summary>
///     Uploads artwork and returns the cached path to reference it by.
///
///     Channel create/update take a LogoPath, which until now could only be produced by the Blazor
///     uploader, so an API client had no way to actually supply an image: it could only point at one
///     already cached on the server. This closes that gap, the same way /api/schedules does for YAML.
/// </summary>
[ApiController]
[EndpointGroupName("general")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public class ArtworkController(IMediator mediator) : ControllerBase
{
    private const long MaxBytes = 10 * 1024 * 1024;

    private static readonly string[] Allowed = ["image/png", "image/jpeg", "image/gif", "image/webp"];

    /// <summary>
    ///     Uploads a channel logo. Send the raw image bytes with the image's Content-Type.
    ///     The returned logoPath and logoContentType go straight into POST/PUT /api/channels.
    /// </summary>
    [HttpPost("/api/artwork/logo", Name = "UploadChannelLogo")]
    [Tags("Artwork")]
    [EndpointSummary("Upload a channel logo")]
    [Consumes("image/png", "image/jpeg", "image/gif", "image/webp")]
    [ProducesResponseType(typeof(ArtworkResponseModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadLogo()
    {
        string contentType = Request.ContentType?.Split(';')[0].Trim().ToLowerInvariant() ?? string.Empty;
        if (!Allowed.Contains(contentType))
        {
            ModelState.AddModelError(
                "contentType",
                $"Content-Type must be one of: {string.Join(", ", Allowed)}");
            return ValidationProblem(ModelState);
        }

        // Buffer with a hard cap rather than trusting Content-Length, which a client controls.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await Request.Body.ReadAsync(chunk, HttpContext.RequestAborted)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                return Problem(
                    $"Logo must be {MaxBytes / 1024 / 1024} MB or smaller",
                    statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), HttpContext.RequestAborted);
        }

        if (buffer.Length == 0)
        {
            ModelState.AddModelError("body", "Logo is empty");
            return ValidationProblem(ModelState);
        }

        buffer.Position = 0;

        Either<BaseError, string> result =
            await mediator.Send(new SaveArtworkToDisk(buffer, ArtworkKind.Logo, contentType));

        return result.Match<IActionResult>(
            path => Created(string.Empty, new ArtworkResponseModel(path, contentType)),
            error => Problem(error.ToString()));
    }

    /// <summary>
    ///     Uploads an image and sets it as a channel's logo, in one call.
    ///
    ///     Use this rather than PUT /api/channels: that is a full replacement, and GET /api/channels
    ///     does not return every updatable field, so a client cannot read-modify-write a channel just
    ///     to change its logo without resetting streaming mode, transcode mode and the rest to their
    ///     defaults. This endpoint changes the logo and nothing else.
    /// </summary>
    [HttpPut("/api/channels/{id:int}/logo", Name = "SetChannelLogo")]
    [Tags("Channels")]
    [EndpointSummary("Upload and set a channel's logo")]
    [Consumes("image/png", "image/jpeg", "image/gif", "image/webp")]
    [ProducesResponseType(typeof(ArtworkResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> SetChannelLogo(int id)
    {
        IActionResult uploaded = await UploadLogo();
        if (uploaded is not CreatedResult { Value: ArtworkResponseModel artwork })
        {
            return uploaded;
        }

        Either<BaseError, Unit> result = await mediator.Send(
            new UpdateChannelLogo(id, artwork.LogoPath, artwork.LogoContentType));

        return result.Match<IActionResult>(
            _ => Ok(artwork),
            error => error.ToString().Contains("does not exist", StringComparison.OrdinalIgnoreCase)
                ? NotFound()
                : Problem(error.ToString()));
    }
}
