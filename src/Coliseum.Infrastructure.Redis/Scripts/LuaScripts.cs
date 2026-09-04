using System.Reflection;

namespace Coliseum.Infrastructure.Redis.Scripts;

/// <summary>
/// Holds the text of the embedded Lua scripts. StackExchange.Redis hashes the script, sends EVALSHA when the
/// server already knows it and transparently falls back to EVAL on NOSCRIPT, so callers just pass the text.
/// Scripts are read once from the assembly at start-up.
/// </summary>
public sealed class LuaScripts
{
    private LuaScripts(string createPlayer, string applyBattle, string markBattle)
    {
        CreatePlayer = createPlayer;
        ApplyBattle = applyBattle;
        MarkBattle = markBattle;
    }

    /// <summary>Atomic "reserve the name, then write the player".</summary>
    public string CreatePlayer { get; }

    /// <summary>Idempotent settlement (ADR-03).</summary>
    public string ApplyBattle { get; }

    /// <summary>Status transition that never overwrites a settled battle.</summary>
    public string MarkBattle { get; }

    public static LuaScripts Load() =>
        new(Read("create_player.lua"), Read("apply_battle.lua"), Read("mark_battle.lua"));

    private static string Read(string fileName)
    {
        string resource = "Coliseum.Redis.Scripts." + fileName;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException("Embedded Lua script not found: " + resource);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
