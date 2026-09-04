using System.Text.Json;
using System.Text.Json.Serialization;
using Coliseum.Contracts.Events;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;

namespace Coliseum.Infrastructure.Redis.Serialization;

/// <summary>
/// Source-generated JSON for what Redis stores or transports: the battle report (inside <c>battle:{id}</c>)
/// and live events (pub/sub). No reflection at runtime (AOT friendly) and the schema is explicit.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(PlayerIdJsonConverter), typeof(BattleIdJsonConverter)])]
[JsonSerializable(typeof(BattleReport))]
[JsonSerializable(typeof(ArenaEvent))]
public sealed partial class ColiseumJsonContext : JsonSerializerContext;

/// <summary>Ids are plain strings on the wire and in storage, never <c>{"value": "..."}</c> objects.</summary>
public sealed class PlayerIdJsonConverter : JsonConverter<PlayerId>
{
    public override PlayerId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        PlayerId.Unchecked(reader.GetString() ?? throw new JsonException("Player id cannot be null."));

    public override void Write(Utf8JsonWriter writer, PlayerId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

public sealed class BattleIdJsonConverter : JsonConverter<BattleId>
{
    public override BattleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        BattleId.Unchecked(reader.GetString() ?? throw new JsonException("Battle id cannot be null."));

    public override void Write(Utf8JsonWriter writer, BattleId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
