using ErsatzTV.Application.MediaCollections;
using ErsatzTV.Application.Playouts;
using ErsatzTV.Core.Domain;

namespace ErsatzTV.Controllers.Api.Models;

/// <summary>
///     Flat api response models. These deliberately do not reuse the internal view models: PlayoutNameViewModel
///     exposes a LanguageExt Option and a PlayoutBuildStatus that carries the whole Playout entity graph, which
///     both leaks internals and blows past the openapi schema depth limit.
/// </summary>
public record CollectionResponseModel(int Id, string Name, bool UseCustomPlaybackOrder)
{
    public static CollectionResponseModel From(MediaCollectionViewModel vm) =>
        new(vm.Id, vm.Name, vm.UseCustomPlaybackOrder);
}

public record PlayoutResponseModel(
    int Id,
    PlayoutScheduleKind ScheduleKind,
    string ChannelNumber,
    string ChannelName,
    string ScheduleName,
    string ScheduleFile,
    TimeSpan? DailyRebuildTime,
    DateTimeOffset? LastBuild,
    bool? LastBuildSuccess,
    string LastBuildMessage)
{
    public static PlayoutResponseModel From(PlayoutNameViewModel vm) =>
        new(
            vm.PlayoutId,
            vm.ScheduleKind,
            vm.ChannelNumber,
            vm.ChannelName,
            vm.ScheduleName,
            vm.ScheduleFile,
            vm.DbDailyRebuildTime,
            vm.BuildStatus?.LastBuild,
            vm.BuildStatus?.Success,
            vm.BuildStatus?.Message);
}
