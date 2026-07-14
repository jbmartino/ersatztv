namespace ErsatzTV.Controllers.Api.Models;

public record CreateClassicPlayoutRequest(int ChannelId, int ProgramScheduleId);

/// <summary>
///     Scripted and external json playouts take a path to a schedule file on the server's disk.
/// </summary>
public record CreateFilePlayoutRequest(int ChannelId, string ScheduleFile);

/// <summary>
///     A sequential playout takes either a ScheduleName, managed through /api/schedules, or an
///     absolute ScheduleFile path on the server. Prefer ScheduleName: a client can upload a schedule
///     and use it without filesystem access to the server, and the database does not end up storing a
///     path that only makes sense on one machine. Exactly one of the two is required.
/// </summary>
public record CreateSequentialPlayoutRequest(int ChannelId)
{
    public string ScheduleName { get; init; }
    public string ScheduleFile { get; init; }
}

public record CreateBlockPlayoutRequest(int ChannelId);
