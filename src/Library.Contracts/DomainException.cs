namespace Library.Contracts;

/// <summary>A refusal with a stable <see cref="Code"/>; the HTTP layer maps the kind to a status.</summary>
public abstract class DomainException(string code, string? detail = null) : Exception(detail ?? code)
{
    public string Code { get; } = code;
}

public class NotFoundException(string code, string? detail = null) : DomainException(code, detail);

public class ConflictException(string code, string? detail = null) : DomainException(code, detail);

public class InvalidRequestException(string code, string? detail = null) : DomainException(code, detail);
