using Library.Contracts;

namespace Library.Lending.Domain;

public sealed class Member(string memberId, string name, string tier)
{
    public string MemberId { get; } = memberId;
    public string Name { get; } = name;
    public string Tier { get; } = tier;
    public int OutstandingFeesCents { get; set; }
}

public sealed class Loan(string loanId, string copyId, string memberId, DateOnly borrowedOn, DateOnly dueOn)
{
    public string LoanId { get; } = loanId;
    public string CopyId { get; } = copyId;
    public string MemberId { get; } = memberId;
    public DateOnly BorrowedOn { get; } = borrowedOn;
    public DateOnly DueOn { get; } = dueOn;
    public DateOnly? ReturnedOn { get; set; }
    public int LateFeeCents { get; set; }
    public bool Active => ReturnedOn is null;
}

public sealed class MemberNotFound(string id) : NotFoundException("member_not_found", id);
public sealed class CopyNotFound() : NotFoundException("copy_not_found");
public sealed class LoanNotFound(string id) : NotFoundException("loan_not_found", id);
public sealed class CopyOnLoan() : ConflictException("copy_on_loan");
public sealed class LoanLimitReached() : ConflictException("loan_limit_reached");
public sealed class FeesOutstanding() : ConflictException("fees_outstanding");
public sealed class LoanAlreadyReturned(string id) : ConflictException("loan_already_returned", id);
public sealed class UnknownTier(string tier) : InvalidRequestException("unknown_tier", tier);

public interface ILendingRepository
{
    void AddMember(Member member);
    Member? GetMember(string memberId);
    void AddLoan(Loan loan);
    Loan? GetLoan(string loanId);
    IReadOnlyList<Loan> ActiveLoansOf(string memberId);
    Loan? ActiveLoanOfCopy(string copyId);
    void RememberCopy(string copyId);
    bool KnowsCopy(string copyId);
}
