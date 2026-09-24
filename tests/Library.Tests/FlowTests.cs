using System.Net;

namespace Library.Tests;

/// <summary>The features end to end through HTTP — and the two contexts meeting only through events.</summary>
public class FlowTests
{
    private static async Task<int> Available(HttpClient c, string q = "domain") =>
        (await (await c.GetAsync($"/catalog/search?q={q}")).Json())[0]!["copies_available"]!.GetValue<int>();

    [Fact, Trait("Rule", "CAT-R1"), Trait("Rule", "CAT-R2")]
    public async Task The_catalog_follows_loans_through_events_only()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        var copy = await c.CopyOf();
        var ada = await c.Member();
        Assert.Equal(1, await Available(c));
        var loan = await (await c.Borrow(ada, copy)).Json();
        Assert.Equal(0, await Available(c));
        await c.PostAsync($"/lending/loans/{loan["loan_id"]}/return", null);
        Assert.Equal(1, await Available(c));
    }

    [Fact, Trait("Rule", "LEND-R1")]
    public async Task One_copy_is_never_lent_twice()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        var copy = await c.CopyOf();
        await c.Borrow(await c.Member(), copy);
        var second = await c.Borrow(await c.Member(), copy);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        Assert.Equal("copy_on_loan", (await second.Json())["code"]!.GetValue<string>());
        var missing = await c.Borrow(await c.Member(), "c_nope");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("copy_not_found", (await missing.Json())["code"]!.GetValue<string>());
    }

    [Fact, Trait("Rule", "LEND-R2")]
    public async Task The_fourth_standard_loan_is_refused_with_the_rule_that_refused_it()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        var ada = await c.Member();
        for (var i = 0; i < 3; i++)
        {
            var r = await c.Borrow(ada, await c.CopyOf($"978000000000{i}", $"Book {i}"));
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }
        var fourth = await c.Borrow(ada, await c.CopyOf("9780000000009", "Book 9"));
        Assert.Equal(HttpStatusCode.Conflict, fourth.StatusCode);
        Assert.Equal("loan_limit_reached", (await fourth.Json())["code"]!.GetValue<string>());
    }

    [Fact, Trait("Rule", "LEND-R3"), Trait("Rule", "LEND-R4"), Trait("Rule", "LEND-R5"), Trait("Rule", "LEND-R6")]
    public async Task Late_return_charges_the_member_blocks_borrowing_and_paying_unblocks()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        var ada = await c.Member();
        var loan = await (await c.Borrow(ada, await c.CopyOf())).Json();
        Assert.Equal("2026-03-16", loan["due_on"]!.GetValue<string>());

        app.Clock.Advance(TimeSpan.FromDays(14 + 3)); // three days late
        var returned = await (await c.PostAsync($"/lending/loans/{loan["loan_id"]}/return", null)).Json();
        Assert.Equal(75, returned["late_fee_cents"]!.GetValue<int>());
        Assert.Equal(75, (await (await c.GetAsync($"/lending/members/{ada}")).Json())["outstanding_fees_cents"]!.GetValue<int>());

        var another = await c.CopyOf("9780134494166", "Clean Architecture");
        Assert.Equal("fees_outstanding", (await (await c.Borrow(ada, another)).Json())["code"]!.GetValue<string>());

        await c.PostAsync($"/lending/members/{ada}/payments", null);
        Assert.Equal(HttpStatusCode.Created, (await c.Borrow(ada, another)).StatusCode);
    }

    [Fact, Trait("Rule", "LEND-R7")]
    public async Task A_loan_is_returned_once()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        var loan = await (await c.Borrow(await c.Member(), await c.CopyOf())).Json();
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync($"/lending/loans/{loan["loan_id"]}/return", null)).StatusCode);
        var again = await c.PostAsync($"/lending/loans/{loan["loan_id"]}/return", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("loan_already_returned", (await again.Json())["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task Search_matches_title_author_or_isbn_and_a_copy_needs_a_book()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        await c.CopyOf();
        foreach (var q in new[] { "DOMAIN", "evans", "9780321125217" })
            Assert.Equal("Domain-Driven Design", (await (await c.GetAsync($"/catalog/search?q={q}")).Json())[0]!["title"]!.GetValue<string>());
        var r = await c.PostAsync("/catalog/books/9999999999/copies", null);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal("book_not_found", (await r.Json())["code"]!.GetValue<string>());
    }

    [Fact, Trait("Rule", "CAT-R3")]
    public async Task Copies_are_listed_with_their_loan_state()
    {
        using var app = new LibraryApp();
        var c = app.CreateClient();
        var first = await c.CopyOf();
        var second = (await (await c.PostAsync("/catalog/books/9780321125217/copies", null)).Json())["copy_id"]!.GetValue<string>();
        await c.Borrow(await c.Member(), first);
        var listed = (await (await c.GetAsync("/catalog/books/9780321125217/copies")).Json()).AsArray()
            .ToDictionary(x => x!["copy_id"]!.GetValue<string>(), x => x!["on_loan"]!.GetValue<bool>());
        Assert.Equal(new Dictionary<string, bool> { [first] = true, [second] = false }, listed);
        var missing = await c.GetAsync("/catalog/books/9999999999/copies");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("book_not_found", (await missing.Json())["code"]!.GetValue<string>());
    }
}
