using Library.Lending.Domain;

namespace Library.Lending.Infrastructure;

public sealed class InMemoryLendingRepository : ILendingRepository
{
    private readonly Dictionary<string, Member> _members = [];
    private readonly Dictionary<string, Loan> _loans = [];
    private readonly HashSet<string> _copies = [];

    public void AddMember(Member member) => _members[member.MemberId] = member;
    public Member? GetMember(string memberId) => _members.GetValueOrDefault(memberId);
    public void AddLoan(Loan loan) => _loans[loan.LoanId] = loan;
    public Loan? GetLoan(string loanId) => _loans.GetValueOrDefault(loanId);
    public IReadOnlyList<Loan> ActiveLoansOf(string memberId) => [.. _loans.Values.Where(l => l.MemberId == memberId && l.Active)];
    public Loan? ActiveLoanOfCopy(string copyId) => _loans.Values.FirstOrDefault(l => l.CopyId == copyId && l.Active);
    public void RememberCopy(string copyId) => _copies.Add(copyId);
    public bool KnowsCopy(string copyId) => _copies.Contains(copyId);
}
