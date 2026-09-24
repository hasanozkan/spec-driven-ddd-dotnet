namespace Library.Lending.Domain;

/// <summary>The lending rules as pure functions — each names the spec rule it implements.</summary>
public static class LendingRules
{
    /// <summary>Refuses with the FIRST rule that fails, in the order the spec lists them.</summary>
    public static void CheckCanBorrow(Member member, Tier tier, bool copyKnown, bool copyOnLoan, int activeLoans)
    {
        if (!copyKnown) throw new CopyNotFound(); // LEND-R1
        if (copyOnLoan) throw new CopyOnLoan(); // LEND-R1
        if (member.OutstandingFeesCents > 0) throw new FeesOutstanding(); // LEND-R4
        if (activeLoans >= tier.MaxActiveLoans) throw new LoanLimitReached(); // LEND-R2
    }

    /// <summary>LEND-R3.</summary>
    public static DateOnly DueOn(Tier tier, DateOnly borrowedOn) => borrowedOn.AddDays(tier.LoanDays);

    /// <summary>LEND-R5: per day late, capped; nothing on or before the due date.</summary>
    public static int LateFeeCents(Tier tier, DateOnly dueOn, DateOnly returnedOn)
    {
        var daysLate = returnedOn.DayNumber - dueOn.DayNumber;
        return daysLate <= 0 ? 0 : Math.Min(daysLate * tier.LateFeePerDayCents, tier.MaxLateFeeCents);
    }
}
