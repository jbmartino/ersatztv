namespace ErsatzTV.Controllers.Api.Models;

public record CreateClassicPlayoutRequest(int ChannelId, int ProgramScheduleId);

/// <summary>
///     Sequential (YAML), scripted, and external json playouts all take a path to a schedule file on disk.
/// </summary>
public record CreateFilePlayoutRequest(int ChannelId, string ScheduleFile);

public record CreateBlockPlayoutRequest(int ChannelId);
