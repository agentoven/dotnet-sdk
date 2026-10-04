using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentOven.Serialization;

/// <summary>
/// A string-backed value set the server owns (status, mode, kind, …). Unlike a C# <c>enum</c>,
/// a value the SDK does not know yet round-trips instead of failing to deserialize, so a newer
/// server never breaks an older client.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "\"Extensible enum\" is the established name for this pattern.")]
public interface IExtensibleEnum<TSelf> where TSelf : struct, IExtensibleEnum<TSelf>
{
    /// <summary>The wire value.</summary>
    string Value { get; }

    /// <summary>Creates an instance from a wire value.</summary>
    static abstract TSelf Create(string value);
}

/// <summary>JSON converter for <see cref="IExtensibleEnum{TSelf}"/> types.</summary>
public sealed class ExtensibleEnumConverter<T> : JsonConverter<T> where T : struct, IExtensibleEnum<T>
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? default : T.Create(reader.GetString() ?? string.Empty);

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value ?? string.Empty);
}
