namespace Coliseum.UnitTests.Architecture;

/// <summary>
/// ARQ-01: a host's Program.cs is composition only. This test keeps it that way by construction: a Program.cs
/// that grows beyond a handful of statements, or that starts branching, fails the build.
/// </summary>
public class HostCompositionRulesTests
{
    private const int MaxStatements = 8;

    [Theory]
    [InlineData("Coliseum.Api")]
    [InlineData("Coliseum.Worker")]
    [InlineData("Coliseum.Mcp")]
    public void Program_cs_is_composition_only(string host)
    {
        string path = Path.Combine(DependencyRulesTests.RepoRoot, "src", host, "Program.cs");
        var statements = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal) && !line.StartsWith("using ", StringComparison.Ordinal))
            .ToList();

        statements.Count.ShouldBeLessThanOrEqualTo(MaxStatements, $"{host}/Program.cs has {statements.Count} statements; move wiring into HostingExtensions");
        statements.ShouldAllBe(line => !line.StartsWith("if ") && !line.StartsWith("switch ") && !line.Contains("=>"), "Program.cs must not contain logic");
    }
}
