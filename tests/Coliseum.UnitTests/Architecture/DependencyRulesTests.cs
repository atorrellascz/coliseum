using System.Xml.Linq;

namespace Coliseum.UnitTests.Architecture;

/// <summary>
/// The dependency rule is enforced by a test, not by convention: Domain and Contracts carry no runtime packages,
/// Domain references no other project, and Application never reaches out to infrastructure or hosts.
/// The test reads the .csproj files straight from the repository so it cannot be fooled by transitive references.
/// </summary>
public class DependencyRulesTests
{
    internal static readonly string RepoRoot = FindRepoRoot();

    [Theory]
    [InlineData("Coliseum.Domain")]
    [InlineData("Coliseum.Contracts")]
    public void Core_projects_have_no_runtime_package_references(string project)
    {
        var runtimePackages = LoadProject(project)
            .Descendants("PackageReference")
            .Where(p => !string.Equals((string?)p.Attribute("PrivateAssets"), "All", StringComparison.OrdinalIgnoreCase))
            .Select(p => (string?)p.Attribute("Include"))
            .ToList();

        runtimePackages.ShouldBeEmpty();
    }

    [Fact]
    public void Domain_references_no_other_project()
    {
        ProjectReferences("Coliseum.Domain").ShouldBeEmpty();
    }

    [Fact]
    public void Contracts_references_no_other_project()
    {
        ProjectReferences("Coliseum.Contracts").ShouldBeEmpty();
    }

    [Fact]
    public void Application_only_depends_on_domain_and_contracts()
    {
        ProjectReferences("Coliseum.Application")
            .ShouldBe(["Coliseum.Domain", "Coliseum.Contracts"], ignoreOrder: true);
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_hosts()
    {
        ProjectReferences("Coliseum.Infrastructure.Redis")
            .ShouldAllBe(name => name == "Coliseum.Application" || name == "Coliseum.Domain" || name == "Coliseum.Contracts");
    }

    private static List<string> ProjectReferences(string project) =>
        LoadProject(project)
            .Descendants("ProjectReference")
            .Select(p => Path.GetFileNameWithoutExtension((string?)p.Attribute("Include") ?? string.Empty))
            .ToList();

    private static XDocument LoadProject(string project) =>
        XDocument.Load(Path.Combine(RepoRoot, "src", project, project + ".csproj"));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Coliseum.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Coliseum.slnx not found above " + AppContext.BaseDirectory);
    }
}
