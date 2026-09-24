namespace Library.Contracts;

// The ONLY things the two contexts share. Changing a field here is a contract
// change between contexts — treat it like an API change.
public sealed record CopyRegistered(string CopyId, string Isbn);

public sealed record LoanOpened(string LoanId, string CopyId, string MemberId, DateOnly DueOn);

public sealed record LoanClosed(string LoanId, string CopyId, string MemberId, int LateFeeCents);
