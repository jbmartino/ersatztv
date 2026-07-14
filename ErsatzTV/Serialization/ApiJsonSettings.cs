using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ErsatzTV.Serialization;

/// <summary>
///     The serializer settings the api actually binds requests with. Shared rather than written inline in Startup so
///     a test can bind a request body exactly the way the running server does. That matters most for
///     <see cref="Controllers.Api.Models.UpdateChannelRequest" />, whose whole contract depends on being able to tell
///     an omitted field from an explicit null, which NullValueHandling decides.
/// </summary>
public static class ApiJsonSettings
{
    public static void Configure(JsonSerializerSettings settings)
    {
        settings.NullValueHandling = NullValueHandling.Ignore;
        settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        settings.ContractResolver = new CustomContractResolver();
        settings.Converters.Add(new StringEnumConverter());
    }

    public static JsonSerializerSettings Create()
    {
        var settings = new JsonSerializerSettings();
        Configure(settings);
        return settings;
    }
}
