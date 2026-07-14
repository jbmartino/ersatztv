using System.Security.Cryptography;
using System.Text;
using ErsatzTV.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ErsatzTV.Filters;

/// <summary>
///     Requires an api key on /api routes when ETV_API_KEY is configured.
///     When it is not configured, every route stays anonymous, which is the historical behavior.
/// </summary>
public class ApiKeyAuthorizationFilter : IAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";
    public const string QueryParameterName = "api_key";
    public const string SecuritySchemeName = "ApiKey";

    private readonly string _apiKey;

    public ApiKeyAuthorizationFilter() : this(SystemEnvironment.ApiKey)
    {
    }

    public ApiKeyAuthorizationFilter(string apiKey) => _apiKey = apiKey;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return;
        }

        PathString path = context.HttpContext.Request.Path;
        if (!IsProtected(path))
        {
            return;
        }

        if (!TryGetPresentedKey(context.HttpContext.Request, out string presented) || !KeysMatch(presented))
        {
            context.Result = new UnauthorizedResult();
        }
    }

    /// <summary>
    ///     The scanner (api/scan/{scanId}) and scripted playout builds (api/scripted/playout/build/{buildId}) are
    ///     called by child processes that have no way to carry credentials. Both already require an unguessable
    ///     GUID that only exists for the duration of the operation.
    /// </summary>
    public static bool IsProtected(PathString path) =>
        path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/api/scan", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/api/scripted", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetPresentedKey(HttpRequest request, out string presented)
    {
        if (request.Headers.TryGetValue(HeaderName, out Microsoft.Extensions.Primitives.StringValues header)
            && !string.IsNullOrWhiteSpace(header))
        {
            presented = header.ToString();
            return true;
        }

        if (request.Query.TryGetValue(QueryParameterName, out Microsoft.Extensions.Primitives.StringValues query)
            && !string.IsNullOrWhiteSpace(query))
        {
            presented = query.ToString();
            return true;
        }

        presented = string.Empty;
        return false;
    }

    private bool KeysMatch(string presented) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented),
            Encoding.UTF8.GetBytes(_apiKey));
}
