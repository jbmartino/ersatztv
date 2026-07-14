using ErsatzTV.Core.Domain;

namespace ErsatzTV.Controllers.Api.Models;

public record CreateLocalLibraryRequest(string Name, LibraryMediaKind MediaKind, List<string> Paths);
