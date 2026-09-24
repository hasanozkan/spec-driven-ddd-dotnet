using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Library.Tests;

public static class Repo
{
    /// <summary>The repository root (where Library.slnx lives), for tests that read specs and snapshots.</summary>
    public static string Root { get; } = Find();

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Library.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}

/// <summary>The app with a clock the test moves: due dates and late fees are about time.</summary>
public sealed class LibraryApp : WebApplicationFactory<Program>
{
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) =>
        builder.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock)));
}

public static class Http
{
    public static async Task<JsonNode> Json(this HttpResponseMessage r) =>
        JsonNode.Parse(await r.Content.ReadAsStringAsync()) ?? throw new InvalidOperationException("empty body");

    public static async Task<string> CopyOf(this HttpClient c, string isbn = "9780321125217", string title = "Domain-Driven Design")
    {
        await c.PostAsJsonAsync("/catalog/books", new { isbn, title, author = "Eric Evans" });
        var r = await c.PostAsync($"/catalog/books/{isbn}/copies", null);
        return (await r.Json())["copy_id"]!.GetValue<string>();
    }

    public static async Task<string> Member(this HttpClient c, string tier = "standard")
    {
        var r = await c.PostAsJsonAsync("/lending/members", new { name = "Ada", tier });
        return (await r.Json())["member_id"]!.GetValue<string>();
    }

    public static Task<HttpResponseMessage> Borrow(this HttpClient c, string memberId, string copyId) =>
        c.PostAsJsonAsync("/lending/loans", new { member_id = memberId, copy_id = copyId });
}
