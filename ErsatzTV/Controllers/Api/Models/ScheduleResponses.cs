namespace ErsatzTV.Controllers.Api.Models;

/// <summary>
///     A sequential (YAML) schedule stored in the config folder. Path is informational: clients
///     address a schedule by name, and never need to know where the server keeps it.
/// </summary>
public record ScheduleResponseModel(string Name, string Path, long SizeBytes, DateTime LastModifiedUtc);
