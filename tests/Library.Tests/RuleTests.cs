using Library.Lending.Domain;

namespace Library.Tests;

/// <summary>The rules as pure functions: no HTTP, no repository, no clock.</summary>
public class RuleTests
{
    private static readonly Tier Standard = Policy.Current["standard"];
    private static readonly Tier Premium = Policy.Current["premium"];

    private static Member Ada(int fees = 0) => new("m", "Ada", "standard") { OutstandingFeesCents = fees };

    [Fact, Trait("Rule", "LEND-R1")]
    public void An_unknown_copy_or_one_already_on_loan_cannot_be_borrowed()
    {
        Assert.Throws<CopyNotFound>(() => LendingRules.CheckCanBorrow(Ada(), Standard, copyKnown: false, copyOnLoan: false, activeLoans: 0));
        Assert.Throws<CopyOnLoan>(() => LendingRules.CheckCanBorrow(Ada(), Standard, copyKnown: true, copyOnLoan: true, activeLoans: 0));
    }

    [Fact, Trait("Rule", "LEND-R2")]
    public void The_tier_caps_active_loans()
    {
        LendingRules.CheckCanBorrow(Ada(), Standard, true, false, activeLoans: 2);
        Assert.Throws<LoanLimitReached>(() => LendingRules.CheckCanBorrow(Ada(), Standard, true, false, activeLoans: 3));
        LendingRules.CheckCanBorrow(Ada(), Premium, true, false, activeLoans: 5);
    }

    [Fact, Trait("Rule", "LEND-R3")]
    public void A_loan_is_due_loan_days_after_it_is_borrowed()
    {
        Assert.Equal(new DateOnly(2026, 3, 16), LendingRules.DueOn(Standard, new DateOnly(2026, 3, 2)));
        Assert.Equal(new DateOnly(2026, 3, 30), LendingRules.DueOn(Premium, new DateOnly(2026, 3, 2)));
    }

    [Fact, Trait("Rule", "LEND-R4")]
    public void Outstanding_fees_block_borrowing_before_the_limit_is_even_looked_at() =>
        Assert.Throws<FeesOutstanding>(() => LendingRules.CheckCanBorrow(Ada(fees: 25), Standard, true, false, activeLoans: 3));

    [Theory, Trait("Rule", "LEND-R5")]
    [InlineData(15, 0)]    // early
    [InlineData(16, 0)]    // on the due date
    [InlineData(17, 25)]   // one day late
    [InlineData(20, 100)]  // four days
    [InlineData(31, 375)]  // fifteen days
    public void The_late_fee_is_per_day_and_capped(int returnedDay, int fee) =>
        Assert.Equal(fee, LendingRules.LateFeeCents(Standard, new DateOnly(2026, 3, 16), new DateOnly(2026, 3, returnedDay)));

    [Fact, Trait("Rule", "LEND-R5")]
    public void The_late_fee_never_exceeds_the_tier_cap() =>
        Assert.Equal(1000, LendingRules.LateFeeCents(Standard, new DateOnly(2026, 3, 16), new DateOnly(2026, 6, 1)));
}
