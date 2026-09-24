using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using Library.Catalog.Application;
using Library.Contracts;
using Library.Lending.Application;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace Library.Tests;

/// <summary>
/// The architecture as tests. The contexts already cannot reference each other
/// (separate assemblies, no project reference); these rules guard the layers
/// inside each context, where the compiler cannot help.
/// </summary>
public class ArchitectureTests
{
    private static readonly Architecture Arch = new ArchLoader()
        .LoadAssemblies(typeof(CatalogService).Assembly, typeof(LendingService).Assembly, typeof(IEventBus).Assembly)
        .Build();

    [Theory]
    [InlineData("Catalog", "Lending")]
    [InlineData("Lending", "Catalog")]
    public void Contexts_do_not_depend_on_each_other(string context, string other) =>
        Types().That().ResideInNamespaceMatching($@"Library\.{context}(\..*)?")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching($@"Library\.{other}(\..*)?"))
            .Check(Arch);

    /// <summary>
    /// The compiler only enforces the boundary while the reference is absent: an
    /// unused ProjectReference compiles fine and opens the door. So the project
    /// files themselves are checked.
    /// </summary>
    [Theory]
    [InlineData("Catalog", "Lending")]
    [InlineData("Lending", "Catalog")]
    public void A_context_project_never_references_the_other(string context, string other)
    {
        var csproj = File.ReadAllText(Path.Combine(Repo.Root, "src", $"Library.{context}", $"Library.{context}.csproj"));
        Assert.DoesNotContain($"Library.{other}", csproj, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Catalog")]
    [InlineData("Lending")]
    public void The_domain_knows_no_framework_and_no_outer_layer(string context) =>
        Types().That().ResideInNamespaceMatching($@"Library\.{context}\.Domain")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching(
                $@"(Microsoft\.AspNetCore.*|Library\.{context}\.(Application|Infrastructure|Api)(\..*)?)"))
            .Check(Arch);

    [Theory]
    [InlineData("Catalog")]
    [InlineData("Lending")]
    public void Application_does_not_reach_up_to_the_api_or_down_to_infrastructure(string context) =>
        Types().That().ResideInNamespaceMatching($@"Library\.{context}\.Application")
            .Should().NotDependOnAny(Types().That().ResideInNamespaceMatching($@"Library\.{context}\.(Api|Infrastructure)(\..*)?"))
            .Check(Arch);
}
