using System.Net.Http.Json;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Library.Tests;

/// <summary>
/// System.Diagnostics.Metrics listeners are process-wide: a meter named "Library"
/// in another test's host would be counted here too. So these tests run alone.
/// </summary>
[CollectionDefinition("metrics", DisableParallelization = true)]
public sealed class RunsAloneForMetrics;

/// <summary>Feature 004: the same specs/telemetry.yaml the Python implementation is held to.</summary>
[Collection("metrics")]
public partial class OperabilityTests
{
    // Label values can contain braces (http_route="/catalog/books/{isbn}/copies"): skip quoted text.
    [GeneratedRegex(@"^(?<name>[a-zA-Z_:][a-zA-Z0-9_:]*)(\{(?<labels>(?:[^""}]|""(?:[^""\\]|\\.)*"")*)\})?\s+(?<value>\S+)")]
    private static partial Regex Sample();

    [GeneratedRegex(@"(?<k>[a-zA-Z_][a-zA-Z0-9_]*)=""(?<v>(?:[^""\\]|\\.)*)""")]
    private static partial Regex Label();

    private sealed record Point(string Name, Dictionary<string, string> Labels, double Value);

    private static async Task<List<Point>> Scrape(HttpClient c)
    {
        var text = await c.GetStringAsync("/metrics");
        return [.. text.Split('\n')
            .Where(l => l.Length > 0 && l[0] != '#')
            .Select(l => Sample().Match(l))
            .Where(m => m.Success)
            .Select(m => new Point(
                m.Groups["name"].Value,
                Label().Matches(m.Groups["labels"].Value).ToDictionary(x => x.Groups["k"].Value, x => x.Groups["v"].Value),
                double.Parse(m.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture)))];
    }

    private static double Value(List<Point> points, string name, params (string K, string V)[] labels) =>
        points.Where(p => p.Name == name && labels.All(l => p.Labels.GetValueOrDefault(l.K) == l.V)).Sum(p => p.Value);

    private static async Task Exercise(LibraryApp app, HttpClient c)
    {
        var ada = await c.Member();
        var loan = await (await c.Borrow(ada, await c.CopyOf())).Json();
        app.Clock.Advance(TimeSpan.FromDays(14 + 2));
        await c.PostAsync($"/lending/loans/{loan["loan_id"]}/return", null);
        await c.Borrow(ada, "c_nope"); // refused
    }

    private sealed class TelemetrySpec
    {
        public List<MetricSpec> Metrics { get; set; } = [];
    }

    private sealed class MetricSpec
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string? Unit { get; set; }
        public List<string> Attributes { get; set; } = [];
        public List<double>? Buckets { get; set; }
    }

    [Fact, Trait("Rule", "OPS-R4")]
    public async Task Every_metric_in_the_telemetry_contract_is_exposed_with_its_attributes()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        await Exercise(app, c);
        var points = await Scrape(c);
        var spec = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build()
            .Deserialize<TelemetrySpec>(File.ReadAllText(Path.Combine(Repo.Root, "specs", "telemetry.yaml")));
        Assert.NotEmpty(spec.Metrics);
        foreach (var m in spec.Metrics)
        {
            var name = m.Name.Replace('.', '_') + (m.Unit == "s" ? "_seconds" : "");
            var family = points.Where(p => p.Name == name || p.Name.StartsWith(name + "_", StringComparison.Ordinal)).ToList();
            Assert.True(family.Count > 0, $"{m.Name} missing as {name}");
            var seen = family.SelectMany(p => p.Labels.Keys).ToHashSet();
            foreach (var attribute in m.Attributes)
                Assert.Contains(attribute.Replace('.', '_'), seen);
            if (m.Buckets is { } buckets)
            {
                var le = family.Where(p => p.Name.EndsWith("_bucket", StringComparison.Ordinal) && p.Labels["le"] != "+Inf")
                    .Select(p => double.Parse(p.Labels["le"], System.Globalization.CultureInfo.InvariantCulture))
                    .Distinct().Order().ToList();
                Assert.Equal(buckets, le);
            }
        }
    }

    [Fact, Trait("Rule", "OPS-R1")]
    public async Task Requests_are_timed_by_route_template_not_raw_path()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        await c.GetAsync("/catalog/books/9999999999/copies");
        var points = await Scrape(c);
        Assert.True(Value(points, "http_server_request_duration_seconds_count",
            ("http_route", "/catalog/books/{isbn}/copies"), ("http_response_status_code", "404")) >= 1);
    }

    [Fact, Trait("Rule", "OPS-R2")]
    public async Task Loans_and_late_fees_are_counted_from_events()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        await Exercise(app, c);
        var points = await Scrape(c);
        Assert.Equal(1, Value(points, "library_loans_opened_total"));
        Assert.Equal(1, Value(points, "library_loans_closed_total", ("library_late", "true")));
        Assert.Equal(50, Value(points, "library_late_fees_cents_total")); // two days late, standard tier
    }

    [Fact, Trait("Rule", "OPS-R3")]
    public async Task Refusals_are_counted_by_problem_code()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        await Exercise(app, c);
        Assert.Equal(1, Value(await Scrape(c), "library_refusals_total", ("library_code", "copy_not_found")));
    }
}
