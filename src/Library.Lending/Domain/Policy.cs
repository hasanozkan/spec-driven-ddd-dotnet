using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Library.Lending.Domain;

public sealed record Tier(string Name, int MaxActiveLoans, int LoanDays, int LateFeePerDayCents, int MaxLateFeeCents);

/// <summary>Tier policy from the embedded copy of specs/policy.yaml (the spec owns the numbers).</summary>
public static class Policy
{
    private static readonly Lazy<IReadOnlyDictionary<string, Tier>> Tiers = new(Load);

    public static IReadOnlyDictionary<string, Tier> Load()
    {
        using var stream = typeof(Policy).Assembly.GetManifestResourceStream("policy.yaml")
            ?? throw new InvalidOperationException("policy.yaml is not embedded");
        using var reader = new StreamReader(stream);
        var doc = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<PolicyDocument>(reader);
        return doc.Tiers.ToDictionary(
            kv => kv.Key,
            kv => new Tier(kv.Key, kv.Value.MaxActiveLoans, kv.Value.LoanDays, kv.Value.LateFeePerDayCents, kv.Value.MaxLateFeeCents));
    }

    public static IReadOnlyDictionary<string, Tier> Current => Tiers.Value;

    private sealed class PolicyDocument
    {
        public Dictionary<string, TierValues> Tiers { get; set; } = [];
    }

    private sealed class TierValues
    {
        public int MaxActiveLoans { get; set; }
        public int LoanDays { get; set; }
        public int LateFeePerDayCents { get; set; }
        public int MaxLateFeeCents { get; set; }
    }
}
