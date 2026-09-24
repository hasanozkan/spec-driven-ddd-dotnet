using Library.Lending.Application;
using Library.Lending.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Library.Lending.Api;

public sealed record RegisterMember(string Name, string Tier = "standard");

public sealed record MemberOut(string MemberId, string Name, string Tier, int OutstandingFeesCents);

public sealed record Borrow(string MemberId, string CopyId);

public sealed record LoanOut(string LoanId, string CopyId, string MemberId, DateOnly BorrowedOn, DateOnly DueOn, DateOnly? ReturnedOn, int LateFeeCents);

public static class LendingEndpoints
{
    public static IEndpointRouteBuilder MapLending(this IEndpointRouteBuilder app)
    {
        var lending = app.MapGroup("/lending").WithTags("lending");

        lending.MapPost("/members", (RegisterMember body, LendingService service) =>
        {
            var m = service.RegisterMember(body.Name, body.Tier);
            return TypedResults.Created($"/lending/members/{m.MemberId}", ToOut(m));
        });
        lending.MapGet("/members/{memberId}", (string memberId, LendingService service) => TypedResults.Ok(ToOut(service.Find(memberId))));
        lending.MapPost("/members/{memberId}/payments", (string memberId, LendingService service) => TypedResults.Ok(ToOut(service.PayFees(memberId))));
        lending.MapPost("/loans", (Borrow body, LendingService service) =>
        {
            var l = service.Borrow(body.MemberId, body.CopyId);
            return TypedResults.Created($"/lending/loans/{l.LoanId}", ToOut(l));
        });
        lending.MapPost("/loans/{loanId}/return", (string loanId, LendingService service) => TypedResults.Ok(ToOut(service.Return(loanId))));
        return app;
    }

    private static MemberOut ToOut(Member m) => new(m.MemberId, m.Name, m.Tier, m.OutstandingFeesCents);

    private static LoanOut ToOut(Loan l) => new(l.LoanId, l.CopyId, l.MemberId, l.BorrowedOn, l.DueOn, l.ReturnedOn, l.LateFeeCents);
}
