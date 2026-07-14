using ErsatzTV.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using NUnit.Framework;
using Shouldly;

namespace ErsatzTV.Tests.Filters;

[TestFixture]
public class ApiKeyAuthorizationFilterTests
{
    private const string Key = "s3cret";

    private static AuthorizationFilterContext ContextFor(
        string path,
        string headerKey = null,
        string queryKey = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;

        if (headerKey is not null)
        {
            httpContext.Request.Headers[ApiKeyAuthorizationFilter.HeaderName] = headerKey;
        }

        if (queryKey is not null)
        {
            httpContext.Request.QueryString =
                new QueryString($"?{ApiKeyAuthorizationFilter.QueryParameterName}={queryKey}");
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    private static IActionResult Run(ApiKeyAuthorizationFilter filter, AuthorizationFilterContext context)
    {
        filter.OnAuthorization(context);
        return context.Result;
    }

    [Test]
    public void Should_Allow_Api_Request_When_No_Key_Is_Configured()
    {
        // this is the historical behavior; configuring no key must not break anyone
        var filter = new ApiKeyAuthorizationFilter(null);

        Run(filter, ContextFor("/api/channels")).ShouldBeNull();
    }

    [Test]
    public void Should_Reject_Api_Request_With_No_Key()
    {
        var filter = new ApiKeyAuthorizationFilter(Key);

        Run(filter, ContextFor("/api/channels")).ShouldBeOfType<UnauthorizedResult>();
    }

    [Test]
    public void Should_Reject_Api_Request_With_Wrong_Key()
    {
        var filter = new ApiKeyAuthorizationFilter(Key);

        Run(filter, ContextFor("/api/channels", headerKey: "nope")).ShouldBeOfType<UnauthorizedResult>();
    }

    [Test]
    public void Should_Allow_Api_Request_With_Correct_Key_Header()
    {
        var filter = new ApiKeyAuthorizationFilter(Key);

        Run(filter, ContextFor("/api/channels", headerKey: Key)).ShouldBeNull();
    }

    [Test]
    public void Should_Allow_Api_Request_With_Correct_Key_Query_Parameter()
    {
        var filter = new ApiKeyAuthorizationFilter(Key);

        Run(filter, ContextFor("/api/channels", queryKey: Key)).ShouldBeNull();
    }

    [Test]
    public void Should_Reject_Write_Requests_Too()
    {
        var filter = new ApiKeyAuthorizationFilter(Key);

        // the whole point: an unauthenticated caller must not be able to delete a channel
        Run(filter, ContextFor("/api/channels/4")).ShouldBeOfType<UnauthorizedResult>();
        Run(filter, ContextFor("/api/playouts/1/build")).ShouldBeOfType<UnauthorizedResult>();
        Run(filter, ContextFor("/api/collections/2/items")).ShouldBeOfType<UnauthorizedResult>();
    }

    [Test]
    public void Should_Exempt_Scanner_Callback()
    {
        // the out-of-process scanner has no way to carry a key; it is protected by a live, random scan id
        var filter = new ApiKeyAuthorizationFilter(Key);

        Run(filter, ContextFor("/api/scan/6c6a1e3e-0000-0000-0000-000000000000/progress")).ShouldBeNull();
    }

    [Test]
    public void Should_Exempt_Scripted_Playout_Build_Callback()
    {
        // likewise for a scripted schedule, which calls back into a short-lived buildId session
        var filter = new ApiKeyAuthorizationFilter(Key);

        Run(filter, ContextFor("/api/scripted/playout/build/6c6a1e3e-0000-0000-0000-000000000000/add_count"))
            .ShouldBeNull();
    }

    [Test]
    public void Should_Not_Protect_Non_Api_Paths()
    {
        // iptv (plex's tuner + guide) has its own optional jwt scheme and must keep working untouched
        var filter = new ApiKeyAuthorizationFilter(Key);

        Run(filter, ContextFor("/iptv/channels.m3u")).ShouldBeNull();
        Run(filter, ContextFor("/iptv/xmltv.xml")).ShouldBeNull();
        Run(filter, ContextFor("/discover.json")).ShouldBeNull();
        Run(filter, ContextFor("/")).ShouldBeNull();
    }

    [Test]
    public void Should_Not_Treat_Api_Prefixed_Paths_As_Api()
    {
        // "/apifoo" is not under "/api"
        var filter = new ApiKeyAuthorizationFilter(Key);

        Run(filter, ContextFor("/apifoo")).ShouldBeNull();
    }
}
