using Library.Catalog.Domain;

namespace Library.Catalog.Infrastructure;

public sealed class InMemoryCatalogRepository : ICatalogRepository
{
    private readonly Dictionary<string, Book> _books = [];
    private readonly Dictionary<string, Copy> _copies = [];

    public void AddBook(Book book) => _books[book.Isbn] = book;

    public Book? GetBook(string isbn) => _books.GetValueOrDefault(isbn);

    public IReadOnlyList<Book> Books() => [.. _books.Values];

    public void AddCopy(Copy copy) => _copies[copy.CopyId] = copy;

    public Copy? GetCopy(string copyId) => _copies.GetValueOrDefault(copyId);

    public IReadOnlyList<Copy> CopiesOf(string isbn) => [.. _copies.Values.Where(c => c.Isbn == isbn)];
}
