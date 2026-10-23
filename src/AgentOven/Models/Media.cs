using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AgentOven;

/// <summary>
/// A file sent with a session message: an image, a PDF or other document, audio (transcribed by the agent's
/// provider), or video. Sent inline as base64, by URL, or by blob-store reference.
/// </summary>
public sealed record Attachment
{
    private Attachment(string type, MediaRef media)
    {
        Type = type;
        Media = media;
    }

    /// <summary><c>image</c>, <c>file</c>, <c>audio</c> or <c>video</c>.</summary>
    public string Type { get; init; }

    /// <summary>The media.</summary>
    public MediaRef Media { get; init; }

    /// <summary>From bytes. The part type is inferred from <paramref name="mimeType"/> (or sniffed from the bytes).</summary>
    public static Attachment FromBytes(ReadOnlySpan<byte> bytes, string? mimeType = null, string? name = null)
    {
        var mime = mimeType ?? MediaTypes.Sniff(bytes) ?? MediaTypes.FromFileName(name) ?? "application/octet-stream";
        return new Attachment(MediaTypes.PartType(mime), new MediaRef(mime) { Data = Convert.ToBase64String(bytes), Name = name });
    }

    /// <summary>From a file on disk.</summary>
    public static async Task<Attachment> FromFileAsync(string path, string? mimeType = null, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return FromBytes(bytes, mimeType ?? MediaTypes.FromFileName(path), Path.GetFileName(path));
    }

    /// <summary>By URL. The provider fetches it.</summary>
    public static Attachment FromUrl(Uri url, string mimeType, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        return new Attachment(MediaTypes.PartType(mimeType), new MediaRef(mimeType) { Url = url.ToString(), Name = name });
    }

    /// <summary>By a key in the control plane's blob store.</summary>
    public static Attachment FromBlob(string blobRef, string mimeType, string? name = null) =>
        new(MediaTypes.PartType(mimeType), new MediaRef(mimeType) { BlobRef = blobRef, Name = name });
}

/// <summary>Where a piece of media lives. Exactly one of <see cref="Data"/>, <see cref="Url"/> and <see cref="BlobRef"/> is set.</summary>
/// <param name="MimeType">MIME type.</param>
public sealed record MediaRef(string MimeType)
{
    /// <summary>Base64 content.</summary>
    public string? Data { get; init; }

    /// <summary>URL.</summary>
    public string? Url { get; init; }

    /// <summary>Blob-store key.</summary>
    public string? BlobRef { get; init; }

    /// <summary>Original file name.</summary>
    public string? Name { get; init; }
}

/// <summary>Audio sent to or returned by an agent: base64 content and its MIME type.</summary>
/// <param name="Data">Base64 content.</param>
/// <param name="MimeType">MIME type, e.g. <c>audio/wav</c> or <c>audio/mpeg</c>.</param>
public sealed record AudioClip(string Data, string MimeType)
{
    /// <summary>From bytes.</summary>
    public static AudioClip FromBytes(ReadOnlySpan<byte> bytes, string? mimeType = null) =>
        new(Convert.ToBase64String(bytes), mimeType ?? MediaTypes.Sniff(bytes) ?? "audio/wav");

    /// <summary>From a file.</summary>
    public static async Task<AudioClip> FromFileAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return FromBytes(bytes, MediaTypes.FromFileName(path));
    }

    /// <summary>The decoded bytes.</summary>
    public byte[] ToBytes() => Convert.FromBase64String(Data);

    /// <summary>Writes the audio to a file.</summary>
    public Task SaveAsync(string path, CancellationToken cancellationToken = default) => File.WriteAllBytesAsync(path, ToBytes(), cancellationToken);
}

/// <summary>MIME type helpers.</summary>
public static class MediaTypes
{
    /// <summary>The session content-part type for a MIME type.</summary>
    public static string PartType(string mimeType) => mimeType switch
    {
        _ when mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) => "image",
        _ when mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) => "audio",
        _ when mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) => "video",
        _ => "file",
    };

    /// <summary>Guesses a MIME type from a file name's extension.</summary>
    public static string? FromFileName(string? fileName) => Path.GetExtension(fileName ?? "").ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        ".md" => "text/markdown",
        ".csv" => "text/csv",
        ".json" => "application/json",
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".m4a" => "audio/mp4",
        ".ogg" => "audio/ogg",
        ".webm" => "audio/webm",
        ".flac" => "audio/flac",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        _ => null,
    };

    /// <summary>Recognises common formats from their first bytes.</summary>
    public static string? Sniff(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47])) return "image/png";
        if (bytes.Length > 2 && bytes[0] == 0xFF && bytes[1] == 0xD8) return "image/jpeg";
        if (bytes.StartsWith("GIF8"u8)) return "image/gif";
        if (bytes.StartsWith("%PDF"u8)) return "application/pdf";
        if (bytes.StartsWith("RIFF"u8) && bytes.Length > 12 && bytes[8..12].SequenceEqual("WAVE"u8)) return "audio/wav";
        if (bytes.StartsWith("RIFF"u8) && bytes.Length > 12 && bytes[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        if (bytes.StartsWith("ID3"u8) || (bytes.Length > 2 && bytes[0] == 0xFF && (bytes[1] & 0xE0) == 0xE0)) return "audio/mpeg";
        if (bytes.StartsWith("OggS"u8)) return "audio/ogg";
        if (bytes.StartsWith("fLaC"u8)) return "audio/flac";
        if (bytes.Length > 8 && bytes[4..8].SequenceEqual("ftyp"u8)) return "video/mp4";
        return null;
    }
}

/// <summary>
/// A provider's modality switches, written to <c>config.modalities</c>. Build one with
/// <c>ProviderModalities.Create().Image().Pdf().Audio(stt: "whisper-1", tts: "tts-1").Build()</c>.
/// On update, modalities you do not mention keep their settings; <see cref="ProviderModalitiesBuilder.Clear"/> removes one.
/// </summary>
public static class ProviderModalities
{
    /// <summary>Every modality name, in the server's order.</summary>
    public static IReadOnlyList<string> All { get; } = ["text", "image", "pdf", "video", "audio", "realtime", "web"];

    /// <summary>Starts a builder.</summary>
    public static ProviderModalitiesBuilder Create() => new();
}

/// <summary>Builds the <c>config.modalities</c> object of a provider.</summary>
public sealed class ProviderModalitiesBuilder
{
    private readonly JsonObject _modalities = [];

    /// <summary>Images in.</summary>
    public ProviderModalitiesBuilder Image(bool enabled = true) => Set("image", enabled);

    /// <summary>PDFs in.</summary>
    public ProviderModalitiesBuilder Pdf(bool enabled = true) => Set("pdf", enabled);

    /// <summary>Video in.</summary>
    public ProviderModalitiesBuilder Video(bool enabled = true) => Set("video", enabled);

    /// <summary>Web search.</summary>
    public ProviderModalitiesBuilder Web(bool enabled = true) => Set("web", enabled);

    /// <summary>Speech in and out, with optional speech-to-text and text-to-speech models.</summary>
    public ProviderModalitiesBuilder Audio(bool enabled = true, string? stt = null, string? tts = null)
    {
        var audio = new JsonObject { ["enabled"] = enabled };
        if (stt is not null) audio["stt_model"] = stt;
        if (tts is not null) audio["tts_model"] = tts;
        _modalities["audio"] = audio;
        return this;
    }

    /// <summary>Realtime voice, with an optional model.</summary>
    public ProviderModalitiesBuilder Realtime(bool enabled = true, string? model = null)
    {
        var realtime = new JsonObject { ["enabled"] = enabled };
        if (model is not null) realtime["model"] = model;
        _modalities["realtime"] = realtime;
        return this;
    }

    /// <summary>Removes a modality's configuration on update (sends <c>null</c>).</summary>
    public ProviderModalitiesBuilder Clear(string modality)
    {
        _modalities[modality] = null;
        return this;
    }

    /// <summary>The <c>config.modalities</c> object.</summary>
    public JsonObject Build() => (JsonObject)_modalities.DeepClone();

    private ProviderModalitiesBuilder Set(string name, bool enabled)
    {
        _modalities[name] = new JsonObject { ["enabled"] = enabled };
        return this;
    }
}

/// <summary>
/// The result of resuming a paused or interrupted run: either completed (like <see cref="InvokeResult"/>) or paused
/// again on another approval-gated tool call.
/// </summary>
public sealed record ResumeResult
{
    /// <summary>completed or paused.</summary>
    public string Status { get; init; } = "";

    /// <summary>Agent.</summary>
    public string? Agent { get; init; }

    /// <summary>Final answer, when completed.</summary>
    public string? Response { get; init; }

    /// <summary>Trace id (the same run).</summary>
    public string TraceId { get; init; } = "";

    /// <summary>Session.</summary>
    public string? SessionId { get; init; }

    /// <summary>Turns.</summary>
    public int? Turns { get; init; }

    /// <summary>Usage.</summary>
    public TokenUsage? Usage { get; init; }

    /// <summary>Latency in milliseconds.</summary>
    public long? LatencyMs { get; init; }

    /// <summary>Execution trace, when completed.</summary>
    public ExecutionTrace? ExecutionTrace { get; init; }

    /// <summary>What is waiting for approval, when paused again.</summary>
    public PendingApproval? Pending { get; init; }

    /// <summary>Paused again.</summary>
    [JsonIgnore]
    public bool IsPaused => string.Equals(Status, "paused", StringComparison.OrdinalIgnoreCase);

    /// <summary>Fields this SDK version does not model.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>A run that is still running or paused, from the control plane's journal.</summary>
public sealed record UnfinishedRun
{
    /// <summary>Trace id; pass it to <see cref="AgentsClient.ResumeAsync"/>.</summary>
    public string TraceId { get; init; } = "";

    /// <summary>Kitchen.</summary>
    public string? Kitchen { get; init; }

    /// <summary>Agent.</summary>
    public string AgentName { get; init; } = "";

    /// <summary>running or paused.</summary>
    public string Status { get; init; } = "";

    /// <summary>Start.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Last journal entry.</summary>
    public DateTimeOffset? LastUpdate { get; init; }

    /// <summary>Turns recorded.</summary>
    public int TurnsLogged { get; init; }
}
