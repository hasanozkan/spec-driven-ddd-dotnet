using Library.Catalog.Application;
using Library.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Library.Catalog.Api;

public sealed record RegisterBook(string Isbn, string Title, string Author);

public sealed record BookOut(string Isbn, string Title, string Author);

public sealed record CopyOut(string CopyId, string Isbn);

public sealed record AvailabilityOut(string Isbn, string Title, string Author, int CopiesTotal, int CopiesAvailable);

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalog(this IEndpointRouteBuilder app)
    {
        var catalog = app.MapGroup("/catalog").WithTags("catalog");

        catalog.MapPost("/books", (RegisterBook body, CatalogService service) =>
        {
            if (body.Isbn is not { Length: >= 10 and <= 17 } || string.IsNullOrWhiteSpace(body.Title) || string.IsNullOrWhiteSpace(body.Author))
            {
                throw new InvalidRequestException("validation_error", "isbn 10-17 chars; title and author required");
            }
            var b = service.RegisterBook(body.Isbn, body.Title, body.Author);
            return TypedResults.Created($"/catalog/books/{b.Isbn}", new BookOut(b.Isbn, b.Title, b.Author));
        });

        catalog.MapPost("/books/{isbn}/copies", (string isbn, CatalogService service) =>
        {
            var c = service.AddCopy(isbn);
            return TypedResults.Created($"/catalog/copies/{c.CopyId}", new CopyOut(c.CopyId, c.Isbn));
        });

        catalog.MapGet("/search", (string q, CatalogService service) =>
            TypedResults.Ok(service.Search(q)
                .Select(a => new AvailabilityOut(a.Book.Isbn, a.Book.Title, a.Book.Author, a.CopiesTotal, a.CopiesAvailable))
                .ToList()));

        return app;
    }
}
