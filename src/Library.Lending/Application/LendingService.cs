using Library.Contracts;
using Library.Lending.Domain;

namespace Library.Lending.Application;

/// <summary>Lending use cases: load, apply the domain rules, save, publish.</summary>
public sealed class LendingService
{
    private readonly ILendingRepository _repo;
    private readonly IReadOnlyDictionary<string, Tier> _policy;
    private readonly IEventBus _bus;
    private readonly TimeProvider _clock;

    public LendingService(ILendingRepository repo, IReadOnlyDictionary<string, Tier> policy, IEventBus bus, TimeProvider clock)
    {
        _repo = repo;
        _policy = policy;
        _bus = bus;
        _clock = clock;
        // Lending's own projection of the catalog: which copies exist.
        bus.Subscribe<CopyRegistered>(e => repo.RememberCopy(e.CopyId));
    }

    private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    public Member RegisterMember(string name, string tier)
    {
        if (!_policy.ContainsKey(tier)) throw new UnknownTier(tier);
        var member = new Member($"m_{Guid.NewGuid():N}"[..12], name, tier);
        _repo.AddMember(member);
        return member;
    }

    public Loan Borrow(string memberId, string copyId)
    {
        var member = Find(memberId);
        var tier = _policy[member.Tier];
        LendingRules.CheckCanBorrow(
            member,
            tier,
            copyKnown: _repo.KnowsCopy(copyId),
            copyOnLoan: _repo.ActiveLoanOfCopy(copyId) is not null,
            activeLoans: _repo.ActiveLoansOf(memberId).Count);
        var loan = new Loan($"l_{Guid.NewGuid():N}"[..12], copyId, memberId, Today, LendingRules.DueOn(tier, Today));
        _repo.AddLoan(loan);
        _bus.Publish(new LoanOpened(loan.LoanId, loan.CopyId, loan.MemberId, loan.DueOn));
        return loan;
    }

    public Loan Return(string loanId)
    {
        var loan = _repo.GetLoan(loanId) ?? throw new LoanNotFound(loanId);
        if (!loan.Active) throw new LoanAlreadyReturned(loanId); // LEND-R7
        var member = Find(loan.MemberId);
        loan.ReturnedOn = Today;
        loan.LateFeeCents = LendingRules.LateFeeCents(_policy[member.Tier], loan.DueOn, Today);
        member.OutstandingFeesCents += loan.LateFeeCents; // LEND-R6
        _bus.Publish(new LoanClosed(loan.LoanId, loan.CopyId, loan.MemberId, loan.LateFeeCents));
        return loan;
    }

    public Member PayFees(string memberId)
    {
        var member = Find(memberId);
        member.OutstandingFeesCents = 0; // LEND-R6
        return member;
    }

    public Member Find(string memberId) => _repo.GetMember(memberId) ?? throw new MemberNotFound(memberId);
}
