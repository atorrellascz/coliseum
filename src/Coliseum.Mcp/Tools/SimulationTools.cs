using System.ComponentModel;
using Coliseum.Domain.Battles;
using Coliseum.Domain.Players;
using ModelContextProtocol.Server;

namespace Coliseum.Mcp.Tools;

/// <summary>
/// What-if tools that run the real battle engine locally (the same <c>Coliseum.Domain</c> package the server uses),
/// with no side effects on the game. An agent can size up an opponent before spending a real battle.
/// </summary>
[McpServerToolType]
public static class SimulationTools
{
    private static readonly DateTimeOffset FixedDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [McpServerTool(Name = "simulate_battle")]
    [Description("Simulate one battle locally with the real engine for the given stats. Deterministic: the same seed always gives the same result. No side effects.")]
    public static SimulationResult SimulateBattle(
        [Description("Attacker attack, 1-10000")] int attackerAttack,
        [Description("Attacker defense, 0-10000")] int attackerDefense,
        [Description("Attacker hit points, 1-10000")] int attackerHitPoints,
        [Description("Defender attack, 1-10000")] int defenderAttack,
        [Description("Defender defense, 0-10000")] int defenderDefense,
        [Description("Defender hit points, 1-10000")] int defenderHitPoints,
        [Description("Seed text; any string. Use the same seed to reproduce a result")] string seed = "sim-1")
    {
        var report = Run(seed, attackerAttack, attackerDefense, attackerHitPoints, defenderAttack, defenderDefense, defenderHitPoints);
        return new SimulationResult(
            report.AttackerWon,
            report.Turns,
            report.AttackerHpRemaining,
            report.DefenderHpRemaining,
            report.Loot.Percent,
            report.Events.Count(e => e.Hit),
            report.Events.Count(e => !e.Hit));
    }

    [McpServerTool(Name = "estimate_win_chance")]
    [Description("Run many local simulations (default 500) for the given stats and return the attacker's win rate and the average number of turns. No side effects.")]
    public static WinChance EstimateWinChance(
        [Description("Attacker attack, 1-10000")] int attackerAttack,
        [Description("Attacker defense, 0-10000")] int attackerDefense,
        [Description("Attacker hit points, 1-10000")] int attackerHitPoints,
        [Description("Defender attack, 1-10000")] int defenderAttack,
        [Description("Defender defense, 0-10000")] int defenderDefense,
        [Description("Defender hit points, 1-10000")] int defenderHitPoints,
        [Description("Number of simulations, 10-5000")] int simulations = 500)
    {
        int runs = Math.Clamp(simulations, 10, 5_000);
        int wins = 0;
        long turns = 0;
        for (int i = 0; i < runs; i++)
        {
            var report = Run($"estimate-{i}", attackerAttack, attackerDefense, attackerHitPoints, defenderAttack, defenderDefense, defenderHitPoints);
            wins += report.AttackerWon ? 1 : 0;
            turns += report.Turns;
        }

        return new WinChance(runs, Math.Round((double)wins / runs, 3), Math.Round((double)turns / runs, 1));
    }

    private static BattleReport Run(string seed, int aAtk, int aDef, int aHp, int dAtk, int dDef, int dHp)
    {
        var attacker = Player.Create(PlayerId.Unchecked("attacker"), "Attacker", null, 0, 0, aAtk, aDef, aHp, FixedDate);
        var defender = Player.Create(PlayerId.Unchecked("defender"), "Defender", null, 0, 0, dAtk, dDef, dHp, FixedDate);
        if (attacker.IsFailure || defender.IsFailure)
        {
            throw new ArgumentException(string.Join("; ", attacker.Errors.Concat(defender.Errors).Select(e => e.Message)));
        }

        var result = BattleEngine.Run(BattleId.Unchecked(Sanitize(seed)), attacker.Value, defender.Value);
        return result.IsSuccess ? result.Value : throw new InvalidOperationException(result.Errors[0].Message);
    }

    /// <summary>Battle ids must match [A-Za-z0-9_-]{1,64}; any other seed text is hashed into that alphabet.</summary>
    private static string Sanitize(string seed)
    {
        var chars = seed.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').Take(64).ToArray();
        return chars.Length > 0 ? new string(chars) : "seed-" + seed.Length;
    }
}

public sealed record SimulationResult(bool AttackerWon, int Turns, int AttackerHpRemaining, int DefenderHpRemaining, int LootPercent, int Hits, int Misses);

public sealed record WinChance(int Simulations, double AttackerWinRate, double AverageTurns);
