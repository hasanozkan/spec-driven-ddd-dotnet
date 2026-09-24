// Composition root: the one place that knows every context. The contexts
// themselves cannot see each other — they are separate assemblies with no
// reference between them.
using System.Text.Json;
using Library.Catalog.Api;
using Library.Catalog.Application;
using Library.Catalog.Domain;
using Library.Catalog.Infrastructure;
using Library.Contracts;
using Library.Lending.Api;
using Library.Lending.Application;
using Library.Lending.Domain;
using Library.Lending.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// The same wire format as the Python implementation: snake_case JSON.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEventBus, InProcessEventBus>();
builder.Services.AddSingleton<ICatalogRepository, InMemoryCatalogRepository>();
builder.Services.AddSingleton<ILendingRepository, InMemoryLendingRepository>();
builder.Services.AddSingleton<CatalogService>();
builder.Services.AddSingleton(sp => new LendingService(
    sp.GetRequiredService<ILendingRepository>(), Policy.Current, sp.GetRequiredService<IEventBus>(), sp.GetRequiredService<TimeProvider>()));

var app = builder.Build();

// Both services subscribe to the bus in their constructors; build them now so
// no event is published before its subscriber exists.
app.Services.GetRequiredService<CatalogService>();
app.Services.GetRequiredService<LendingService>();

app.UseExceptionHandler(errors => errors.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, code, detail) = error switch
    {
        NotFoundException e => (StatusCodes.Status404NotFound, e.Code, e.Message),
        ConflictException e => (StatusCodes.Status409Conflict, e.Code, e.Message),
        DomainException e => (StatusCodes.Status422UnprocessableEntity, e.Code, e.Message),
        BadHttpRequestException => (StatusCodes.Status422UnprocessableEntity, "validation_error", "malformed request"),
        _ => (StatusCodes.Status500InternalServerError, "internal_error", "unexpected error"),
    };
    context.Response.StatusCode = status;
    // WriteAsJsonAsync sets its own content type; pass the problem type explicitly.
    await context.Response.WriteAsJsonAsync(
        new { type = "about:blank", status, code, detail }, options: null, contentType: "application/problem+json");
}));

app.MapOpenApi();
app.MapGet("/healthz", () => TypedResults.Ok(new { status = "ok" })).ExcludeFromDescription();
app.MapCatalog();
app.MapLending();

app.Run();

public partial class Program;
