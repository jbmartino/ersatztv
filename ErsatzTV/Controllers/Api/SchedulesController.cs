using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using ErsatzTV.Controllers.Api.Models;
using ErsatzTV.Core;
using ErsatzTV.Core.Interfaces.Scheduling;
using ErsatzTV.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErsatzTV.Controllers.Api;

/// <summary>
///     Sequential (YAML) schedules, stored in the config folder rather than the database.
///     Uploading the content over HTTP is what lets a client manage scheduling without filesystem
///     access to the server, so config-as-code works the same whether ErsatzTV runs on a desktop,
///     in Docker, or in a cluster.
/// </summary>
[ApiController]
[EndpointGroupName("general")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public partial class SchedulesController(
    ISequentialScheduleValidator validator,
    IDbContextFactory<TvContext> dbContextFactory)
    : ControllerBase
{
    private const string Extension = ".seq.yaml";

    [HttpGet("/api/schedules", Name = "GetSchedules")]
    [Tags("Schedules")]
    [EndpointSummary("Get all sequential schedules")]
    [ProducesResponseType(typeof(List<ScheduleResponseModel>), StatusCodes.Status200OK)]
    public ActionResult<List<ScheduleResponseModel>> GetAll()
    {
        if (!Directory.Exists(FileSystemLayout.SchedulesFolder))
        {
            return new List<ScheduleResponseModel>();
        }

        List<ScheduleResponseModel> schedules = Directory
            .EnumerateFiles(FileSystemLayout.SchedulesFolder, "*" + Extension)
            .Select(path => new FileInfo(path))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new ScheduleResponseModel(
                NameFromFileName(file.Name),
                file.FullName,
                file.Length,
                file.LastWriteTimeUtc))
            .ToList();

        return schedules;
    }

    [HttpGet("/api/schedules/{name}", Name = "GetSchedule")]
    [Tags("Schedules")]
    [EndpointSummary("Get the YAML content of a sequential schedule")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOne(string name)
    {
        if (!TryResolvePath(name, out string path))
        {
            return Problem(InvalidNameMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        string yaml = await System.IO.File.ReadAllTextAsync(path, HttpContext.RequestAborted);
        return Content(yaml, "text/plain");
    }

    /// <summary>
    ///     Creates or replaces a schedule. The YAML is validated against the same schema the playout
    ///     builder uses, so an invalid schedule is rejected here rather than failing at build time.
    /// </summary>
    [HttpPut("/api/schedules/{name}", Name = "PutSchedule")]
    [Tags("Schedules")]
    [EndpointSummary("Create or replace a sequential schedule")]
    [Consumes("text/plain", "application/yaml", "application/x-yaml")]
    [ProducesResponseType(typeof(ScheduleResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put(string name)
    {
        if (!TryResolvePath(name, out string path))
        {
            return Problem(InvalidNameMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        // Read the body directly: there is no text/plain input formatter by default, and registering
        // one globally would change model binding for every other endpoint.
        using var reader = new StreamReader(Request.Body);
        string yaml = await reader.ReadToEndAsync(HttpContext.RequestAborted);

        if (string.IsNullOrWhiteSpace(yaml))
        {
            ModelState.AddModelError("yaml", "Schedule is empty");
            return ValidationProblem(ModelState);
        }

        if (!await validator.ValidateSchedule(yaml, isImport: false))
        {
            IList<string> messages = await validator.GetValidationMessages(yaml, isImport: false);
            foreach (string message in messages)
            {
                ModelState.AddModelError("yaml", message);
            }

            return ValidationProblem(ModelState);
        }

        Directory.CreateDirectory(FileSystemLayout.SchedulesFolder);
        await System.IO.File.WriteAllTextAsync(path, yaml, HttpContext.RequestAborted);

        var file = new FileInfo(path);
        return Ok(new ScheduleResponseModel(name, file.FullName, file.Length, file.LastWriteTimeUtc));
    }

    /// <summary>
    ///     Deletes a schedule. Refuses while a playout still references it, since the playout would
    ///     fail on its next build.
    /// </summary>
    [HttpDelete("/api/schedules/{name}", Name = "DeleteSchedule")]
    [Tags("Schedules")]
    [EndpointSummary("Delete a sequential schedule")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string name)
    {
        if (!TryResolvePath(name, out string path))
        {
            return Problem(InvalidNameMessage, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        await using TvContext dbContext = await dbContextFactory.CreateDbContextAsync(HttpContext.RequestAborted);
        List<string> inUse = await dbContext.Playouts
            .AsNoTracking()
            .Where(p => p.ScheduleFile == path)
            .Select(p => p.Channel.Name)
            .ToListAsync(HttpContext.RequestAborted);

        if (inUse.Count > 0)
        {
            return Problem(
                $"Schedule is in use by: {string.Join(", ", inUse)}",
                statusCode: StatusCodes.Status409Conflict);
        }

        System.IO.File.Delete(path);
        return Ok();
    }

    /// <summary>
    ///     Resolves a schedule name to a path inside the schedules folder. The name reaches the
    ///     filesystem, so anything that is not a bare, safe name is rejected outright rather than
    ///     sanitized: "../../ErsatzTV/appsettings.json" must never resolve.
    /// </summary>
    internal static bool TryResolvePath(string name, out string path)
    {
        path = null;

        if (string.IsNullOrWhiteSpace(name) || !SafeName().IsMatch(name))
        {
            return false;
        }

        string candidate = Path.GetFullPath(Path.Combine(FileSystemLayout.SchedulesFolder, name + Extension));

        // Belt and braces: even with a name that matched, the result must stay in the folder.
        string root = Path.GetFullPath(FileSystemLayout.SchedulesFolder) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.Ordinal))
        {
            return false;
        }

        path = candidate;
        return true;
    }

    private static string NameFromFileName(string fileName) =>
        fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^Extension.Length]
            : Path.GetFileNameWithoutExtension(fileName);

    private const string InvalidNameMessage =
        "Schedule name must be 1-64 characters of letters, numbers, dot, dash or underscore, and cannot start with a dot";

    [GeneratedRegex(@"^(?!\.)[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SafeName();
}
