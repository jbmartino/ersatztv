using Newtonsoft.Json;

namespace ErsatzTV.Core.Api.Channels;

public record ChannelResponseModel(
    int Id,
    string Number,
    string Name,
    [property: JsonProperty("ffmpegProfile")]
    string FFmpegProfile,
    string Language,
    string StreamingMode,
    // Artwork is content-addressed, so a client that hashes its local image can tell whether the
    // channel already has that exact logo and skip the upload. Without this, reconciling a logo
    // would mean re-uploading it on every run.
    string LogoPath);
