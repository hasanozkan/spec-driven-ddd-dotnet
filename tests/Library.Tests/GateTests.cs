using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Library.Tests;

/// <summary>The build gates, as tests: traceability, the policy mirror and the contract snapshot.</summary>
public partial class GateTests
{
    [GeneratedRegex(@"\*\*([A-Z]+-R\d+)\*\*")]
    private static partial Regex RuleInSpec();

    [Fact]
    public void Every_spec_rule_has_a_test_and_every_test_cites_a_real_rule()
    {
        var specRules = Directory.GetFiles(Path.Combine(Repo.Root, "specs", "features"), "README.md", SearchOption.AllDirectories)
            .SelectMany(f => RuleInSpec().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet();
        var tested = typeof(GateTests).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods())
            .SelectMany(m => m.GetCustomAttributesData())
            .Where(a => a.AttributeType == typeof(TraitAttribute) && (string?)a.ConstructorArguments[0].Value == "Rule")
            .Select(a => (string)a.ConstructorArguments[1].Value!)
            .ToHashSet();
        Assert.NotEmpty(specRules);
        Assert.Empty(specRules.Except(tested)); // a rule nobody proves
        Assert.Empty(tested.Except(specRules)); // a test proving a rule nobody wrote
    }

    [Fact]
    public void The_embedded_policy_is_byte_identical_to_the_spec()
    {
        var spec = File.ReadAllBytes(Path.Combine(Repo.Root, "specs", "policy.yaml"));
        using var stream = typeof(Library.Lending.Domain.Policy).Assembly.GetManifestResourceStream("policy.yaml")!;
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        Assert.Equal(spec, copy.ToArray());
    }

    /// <summary>An API change must arrive with its snapshot. UPDATE_CONTRACTS=1 rewrites it.</summary>
    [Fact]
    public async Task The_openapi_document_matches_the_committed_snapshot()
    {
        using var app = new LibraryApp();
        var live = JsonNode.Parse(await app.CreateClient().GetStringAsync("/openapi/v1.json"))!;
        live.AsObject().Remove("servers"); // host-dependent
        var text = live.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
        var path = Path.Combine(Repo.Root, "contracts", "openapi.json");
        if (Environment.GetEnvironmentVariable("UPDATE_CONTRACTS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        Assert.True(File.Exists(path), "contracts/openapi.json missing — run `make contracts-update`");
        Assert.Equal(File.ReadAllText(path), text);
    }
}
