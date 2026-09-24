using Library.Contracts;

namespace Library.Catalog.Domain;

public sealed record Book(string Isbn, string Title, string Author);

public sealed class Copy(string copyId, string isbn)
{
    public string CopyId { get; } = copyId;

    public string Isbn { get; } = isbn;

    public bool OnLoan { get; set; }
}

/// <summary>What a reader sees: a view, not an invariant (specs/domain/bounded_contexts.md).</summary>
public sealed record Availability(Book Book, int CopiesTotal, int CopiesAvailable)
{
    public static Availability Of(Book book, IReadOnlyCollection<Copy> copies) =>
        new(book, copies.Count, copies.Count(c => !c.OnLoan));
}

public sealed class BookNotFound(string isbn) : NotFoundException("book_not_found", isbn);

public sealed class BookAlreadyRegistered(string isbn) : ConflictException("book_already_registered", isbn);

public interface ICatalogRepository
{
    void AddBook(Book book);

    Book? GetBook(string isbn);

    IReadOnlyList<Book> Books();

    void AddCopy(Copy copy);

    Copy? GetCopy(string copyId);

    IReadOnlyList<Copy> CopiesOf(string isbn);
}
