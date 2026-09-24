using Library.Catalog.Domain;
using Library.Contracts;

namespace Library.Catalog.Application;

/// <summary>Availability is a projection of Lending's events (CAT-R1, CAT-R2): the catalog never asks Lending.</summary>
public sealed class CatalogService
{
    private readonly ICatalogRepository _repo;
    private readonly IEventBus _bus;

    public CatalogService(ICatalogRepository repo, IEventBus bus)
    {
        _repo = repo;
        _bus = bus;
        bus.Subscribe<LoanOpened>(e => SetOnLoan(e.CopyId, true));
        bus.Subscribe<LoanClosed>(e => SetOnLoan(e.CopyId, false));
    }

    public Book RegisterBook(string isbn, string title, string author)
    {
        if (_repo.GetBook(isbn) is not null)
        {
            throw new BookAlreadyRegistered(isbn);
        }
        var book = new Book(isbn, title, author);
        _repo.AddBook(book);
        return book;
    }

    public Copy AddCopy(string isbn)
    {
        if (_repo.GetBook(isbn) is null)
        {
            throw new BookNotFound(isbn);
        }
        var copy = new Copy($"c_{Guid.NewGuid():N}"[..12], isbn);
        _repo.AddCopy(copy);
        _bus.Publish(new CopyRegistered(copy.CopyId, isbn));
        return copy;
    }

    /// <summary>CAT-R3.</summary>
    public IReadOnlyList<Copy> Copies(string isbn) =>
        _repo.GetBook(isbn) is null ? throw new BookNotFound(isbn) : _repo.CopiesOf(isbn);

    public IReadOnlyList<Availability> Search(string text) =>
        [.. _repo.Books()
            .Where(b => b.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                || b.Author.Contains(text, StringComparison.OrdinalIgnoreCase)
                || b.Isbn == text)
            .Select(b => Availability.Of(b, _repo.CopiesOf(b.Isbn)))];

    private void SetOnLoan(string copyId, bool onLoan)
    {
        if (_repo.GetCopy(copyId) is { } copy)
        {
            copy.OnLoan = onLoan;
        }
    }
}
