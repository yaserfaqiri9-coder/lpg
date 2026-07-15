namespace PTGOilSystem.Web.Data;

/// <summary>
/// Provable, additive backfill of PaymentTransactions.CompanyId from existing
/// relations only. Every statement touches exclusively rows whose CompanyId is
/// still NULL, so the statements are idempotent and never overwrite data.
///
/// Provable sources, in order:
///   1. The payment's own contract.
///   2. The linked sales transaction's explicit company.
///   3. The linked sales transaction's contract.
///   4. The linked expense transaction's contract.
///   5. The payment's shipment, when every contract of that shipment
///      (junction rows plus the legacy primary contract) belongs to exactly
///      one company.
///   6. The linked expense transaction's shipment, under the same
///      single-company condition.
///
/// Anything else (sarraf-only, driver-only, employee-only, manual payments,
/// multi-company shipments) stays NULL and is surfaced by the ownership report
/// instead of being guessed.
/// </summary>
public static class PaymentCompanyBackfillSql
{
    private const string SingleCompanyShipmentCte = """
        WITH shipment_company AS (
            SELECT s."ShipmentId", MIN(s."CompanyId") AS "CompanyId"
            FROM (
                SELECT sc."ShipmentId", ct."CompanyId"
                FROM "ShipmentContracts" sc
                JOIN "Contracts" ct ON ct."Id" = sc."ContractId"
                UNION
                SELECT sh."Id" AS "ShipmentId", ct."CompanyId"
                FROM "Shipments" sh
                JOIN "Contracts" ct ON ct."Id" = sh."ContractId"
            ) s
            GROUP BY s."ShipmentId"
            HAVING COUNT(DISTINCT s."CompanyId") = 1
        )
        """;

    public static readonly string[] Statements =
    [
        // 1. Payment bound directly to a contract.
        """
        UPDATE "PaymentTransactions" p
        SET "CompanyId" = c."CompanyId"
        FROM "Contracts" c
        WHERE p."CompanyId" IS NULL AND p."ContractId" = c."Id";
        """,

        // 2. Payment bound to a sales transaction that carries an explicit company.
        """
        UPDATE "PaymentTransactions" p
        SET "CompanyId" = st."CompanyId"
        FROM "SalesTransactions" st
        WHERE p."CompanyId" IS NULL
          AND p."SalesTransactionId" = st."Id"
          AND st."CompanyId" IS NOT NULL;
        """,

        // 3. Payment bound to a sales transaction whose contract determines the company.
        """
        UPDATE "PaymentTransactions" p
        SET "CompanyId" = c."CompanyId"
        FROM "SalesTransactions" st
        JOIN "Contracts" c ON c."Id" = st."ContractId"
        WHERE p."CompanyId" IS NULL AND p."SalesTransactionId" = st."Id";
        """,

        // 4. Payment bound to an expense transaction whose contract determines the company.
        """
        UPDATE "PaymentTransactions" p
        SET "CompanyId" = c."CompanyId"
        FROM "ExpenseTransactions" e
        JOIN "Contracts" c ON c."Id" = e."ContractId"
        WHERE p."CompanyId" IS NULL AND p."ExpenseTransactionId" = e."Id";
        """,

        // 5. Payment bound to a shipment whose contracts all belong to one company.
        SingleCompanyShipmentCte + """
        UPDATE "PaymentTransactions" p
        SET "CompanyId" = sc."CompanyId"
        FROM shipment_company sc
        WHERE p."CompanyId" IS NULL AND p."ShipmentId" = sc."ShipmentId";
        """,

        // 6. Payment bound to an expense on a single-company shipment.
        SingleCompanyShipmentCte + """
        UPDATE "PaymentTransactions" p
        SET "CompanyId" = sc."CompanyId"
        FROM "ExpenseTransactions" e
        JOIN shipment_company sc ON sc."ShipmentId" = e."ShipmentId"
        WHERE p."CompanyId" IS NULL AND p."ExpenseTransactionId" = e."Id";
        """
    ];
}
