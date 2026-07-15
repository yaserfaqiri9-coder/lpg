using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Models.Entities;

namespace PTGOilSystem.Web.Services.Accounting;

public sealed record CompanyOwnershipKindCount(PaymentKind PaymentKind, int Count);

public sealed record CompanyOwnershipReport(
    int TotalPayments,
    int PaymentsWithCompany,
    int PaymentsWithoutCompany,
    IReadOnlyList<CompanyOwnershipKindCount> AmbiguousPaymentsByKind,
    int TotalCashAccounts,
    int CashAccountsWithCompany,
    int CashAccountsWithoutCompany);

public interface ICompanyOwnershipReportService
{
    Task<CompanyOwnershipReport> BuildAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only report of company-ownership coverage after the provable backfill.
/// Rows without a company are exactly the ones the new GL must not touch until
/// their ownership is confirmed; nothing here mutates data.
/// </summary>
public sealed class CompanyOwnershipReportService(ApplicationDbContext db) : ICompanyOwnershipReportService
{
    public async Task<CompanyOwnershipReport> BuildAsync(CancellationToken cancellationToken = default)
    {
        var totalPayments = await db.PaymentTransactions.AsNoTracking().CountAsync(cancellationToken);
        var paymentsWithCompany = await db.PaymentTransactions.AsNoTracking()
            .CountAsync(x => x.CompanyId != null, cancellationToken);

        var ambiguousByKind = (await db.PaymentTransactions.AsNoTracking()
                .Where(x => x.CompanyId == null)
                .GroupBy(x => x.PaymentKind)
                .Select(g => new { Kind = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken))
            .OrderBy(x => x.Kind)
            .Select(x => new CompanyOwnershipKindCount(x.Kind, x.Count))
            .ToList();

        var totalCashAccounts = await db.CashAccounts.AsNoTracking().CountAsync(cancellationToken);
        var cashAccountsWithCompany = await db.CashAccounts.AsNoTracking()
            .CountAsync(x => x.CompanyId != null, cancellationToken);

        return new CompanyOwnershipReport(
            totalPayments,
            paymentsWithCompany,
            totalPayments - paymentsWithCompany,
            ambiguousByKind,
            totalCashAccounts,
            cashAccountsWithCompany,
            totalCashAccounts - cashAccountsWithCompany);
    }
}
