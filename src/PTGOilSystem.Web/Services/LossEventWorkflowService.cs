using Microsoft.EntityFrameworkCore;
using PTGOilSystem.Web.Data;
using PTGOilSystem.Web.Helpers;
using PTGOilSystem.Web.Models.Entities;
using PTGOilSystem.Web.Models.LossEvents;
using PTGOilSystem.Web.Services.Audit;
using PTGOilSystem.Web.Services.Exceptions;

namespace PTGOilSystem.Web.Services;

public sealed class LossEventWorkflowService : ILossEventWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly IStockService _stock;
    private readonly IAuditService _audit;
    private readonly Accounting.IInventoryLossAccountingAdapter? _lossAccounting;

    public LossEventWorkflowService(
        ApplicationDbContext db,
        IStockService stock,
        IAuditService audit,
        Accounting.IInventoryLossAccountingAdapter? lossAccounting = null)
    {
        _db = db;
        _stock = stock;
        _audit = audit;
        _lossAccounting = lossAccounting;
    }

    public LossEventComputation ComputeMetrics(
        decimal expectedQuantityMt,
        decimal actualQuantityMt,
        decimal toleranceQuantityMt)
    {
        var differenceQuantityMt = expectedQuantityMt - actualQuantityMt;
        var positiveLossMt = Math.Max(differenceQuantityMt, 0m);
        var allowableLossMt = Math.Min(positiveLossMt, toleranceQuantityMt);
        var chargeableLossMt = Math.Max(0m, positiveLossMt - toleranceQuantityMt);
        return new LossEventComputation(differenceQuantityMt, allowableLossMt, chargeableLossMt);
    }

    public async Task ValidateAsync(
        LossEventSubmission submission,
        Action<string, string> addError,
        CancellationToken ct = default)
    {
        NormalizeSubmission(submission);

        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == submission.ProductId && p.IsActive, ct);
        if (product is null)
        {
            addError(nameof(submission.ProductId), "کالای انتخاب‌شده معتبر نیست.");
        }

        if (submission.ContractId.HasValue)
        {
            var contract = await _db.Contracts
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == submission.ContractId.Value, ct);
            if (contract is null)
            {
                addError(nameof(submission.ContractId), "قرارداد انتخاب‌شده معتبر نیست.");
            }
            else if (contract.ProductId != submission.ProductId)
            {
                addError(nameof(submission.ContractId), "قرارداد انتخاب‌شده با کالای انتخابی هم‌خوان نیست.");
            }
        }

        if (submission.ShipmentId.HasValue)
        {
            var shipment = await _db.Shipments
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == submission.ShipmentId.Value, ct);
            if (shipment is null)
            {
                addError(nameof(submission.ShipmentId), "Shipment انتخاب‌شده معتبر نیست.");
            }
            else if (submission.ContractId.HasValue && shipment.ContractId != submission.ContractId)
            {
                addError(nameof(submission.ShipmentId), "Shipment انتخاب‌شده با قرارداد انتخابی هم‌خوان نیست.");
            }
        }

        if (submission.LoadingRegisterId.HasValue)
        {
            var loading = await _db.LoadingRegisters
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == submission.LoadingRegisterId.Value, ct);
            if (loading is null)
            {
                addError(nameof(submission.LoadingRegisterId), "بارگیری انتخاب‌شده معتبر نیست.");
            }
            else
            {
                if (loading.ProductId != submission.ProductId)
                {
                    addError(nameof(submission.LoadingRegisterId), "بارگیری انتخاب‌شده با کالای انتخابی هم‌خوان نیست.");
                }

                if (submission.ContractId.HasValue && loading.ContractId != submission.ContractId)
                {
                    addError(nameof(submission.LoadingRegisterId), "بارگیری انتخاب‌شده با قرارداد انتخابی هم‌خوان نیست.");
                }
            }
        }

        if (submission.LoadingReceiptId.HasValue)
        {
            var receipt = await _db.LoadingReceipts
                .AsNoTracking()
                .Include(r => r.LoadingRegister)
                .FirstOrDefaultAsync(r => r.Id == submission.LoadingReceiptId.Value, ct);
            if (receipt is null)
            {
                addError(nameof(submission.LoadingReceiptId), "رسید بارگیری انتخاب‌شده معتبر نیست.");
            }
            else if (receipt.LoadingRegister is not null)
            {
                if (receipt.LoadingRegister.ProductId != submission.ProductId)
                {
                    addError(nameof(submission.LoadingReceiptId), "رسید بارگیری انتخاب‌شده با کالای انتخابی هم‌خوان نیست.");
                }

                if (submission.ContractId.HasValue && receipt.LoadingRegister.ContractId != submission.ContractId)
                {
                    addError(nameof(submission.LoadingReceiptId), "رسید بارگیری انتخاب‌شده با قرارداد انتخابی هم‌خوان نیست.");
                }
            }
        }

        if (submission.TruckDispatchId.HasValue)
        {
            var dispatch = await _db.TruckDispatches
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == submission.TruckDispatchId.Value, ct);
            if (dispatch is null)
            {
                addError(nameof(submission.TruckDispatchId), "دیسپچ انتخاب‌شده معتبر نیست.");
            }
            else
            {
                if (dispatch.ProductId != submission.ProductId)
                {
                    addError(nameof(submission.TruckDispatchId), "دیسپچ انتخاب‌شده با کالای انتخابی هم‌خوان نیست.");
                }

                if (submission.ContractId.HasValue && dispatch.ContractId != submission.ContractId)
                {
                    addError(nameof(submission.TruckDispatchId), "دیسپچ انتخاب‌شده با قرارداد انتخابی هم‌خوان نیست.");
                }
            }
        }

        if (submission.SalesTransactionId.HasValue)
        {
            var sale = await _db.SalesTransactions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == submission.SalesTransactionId.Value, ct);
            if (sale is null)
            {
                addError(nameof(submission.SalesTransactionId), "فروش انتخاب‌شده معتبر نیست.");
            }
            else
            {
                if (sale.ProductId != submission.ProductId)
                {
                    addError(nameof(submission.SalesTransactionId), "فروش انتخاب‌شده با کالای انتخابی هم‌خوان نیست.");
                }

                if (submission.ContractId.HasValue && sale.ContractId != submission.ContractId)
                {
                    addError(nameof(submission.SalesTransactionId), "فروش انتخاب‌شده با قرارداد انتخابی هم‌خوان نیست.");
                }
            }
        }

        if (submission.AffectsInventory)
        {
            if (!CanAffectInventory(submission.Stage))
            {
                addError(nameof(submission.AffectsInventory), "این مرحله فقط برای گزارش است و نباید موجودی را دوباره کم کند.");
            }

            if (!submission.TerminalId.HasValue)
            {
                addError(nameof(submission.TerminalId), "برای ثبت اثر روی موجودی، انتخاب ترمینال الزامی است.");
            }

            if (!submission.StorageTankId.HasValue)
            {
                addError(nameof(submission.StorageTankId), "برای ثبت اثر روی موجودی، انتخاب مخزن الزامی است.");
            }
        }

        if (submission.TerminalId.HasValue)
        {
            var terminal = await _db.Terminals
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == submission.TerminalId.Value && t.IsActive, ct);
            if (terminal is null)
            {
                addError(nameof(submission.TerminalId), "ترمینال انتخاب‌شده معتبر نیست.");
            }
        }

        if (submission.StorageTankId.HasValue)
        {
            var tank = await _db.StorageTanks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == submission.StorageTankId.Value, ct);
            if (tank is null)
            {
                addError(nameof(submission.StorageTankId), "مخزن انتخاب‌شده معتبر نیست.");
            }
            else
            {
                if (submission.TerminalId.HasValue && tank.TerminalId != submission.TerminalId.Value)
                {
                    addError(nameof(submission.StorageTankId), "مخزن انتخاب‌شده به ترمینال انتخابی تعلق ندارد.");
                }

                if (tank.ProductId.HasValue && tank.ProductId != submission.ProductId)
                {
                    addError(nameof(submission.StorageTankId), "مخزن انتخاب‌شده برای کالای انتخابی تعریف نشده است.");
                }
            }
        }

        if (submission.ToleranceQuantityMt < 0m)
        {
            addError(nameof(submission.ToleranceQuantityMt), "تلورانس نامعتبر است.");
        }

        var metrics = ComputeMetrics(
            submission.ExpectedQuantityMt,
            submission.ActualQuantityMt,
            submission.ToleranceQuantityMt);
        var inventoryLossMt = Math.Max(metrics.DifferenceQuantityMt, 0m);

        if (submission.AffectsInventory && inventoryLossMt <= 0m)
        {
            addError(nameof(submission.AffectsInventory), "برای کاهش موجودی باید اختلاف مثبت باشد.");
        }
    }

    public async Task<LossEventWorkflowResult> CreateAsync(
        LossEventSubmission submission,
        CancellationToken ct = default)
    {
        NormalizeSubmission(submission);
        var metrics = ComputeMetrics(
            submission.ExpectedQuantityMt,
            submission.ActualQuantityMt,
            submission.ToleranceQuantityMt);
        var inventoryLossMt = Math.Max(metrics.DifferenceQuantityMt, 0m);

        InventoryMovement? movement = null;
        if (submission.AffectsInventory)
        {
            movement = new InventoryMovement
            {
                ProductId = submission.ProductId,
                ContractId = submission.ContractId,
                TerminalId = submission.TerminalId!.Value,
                StorageTankId = submission.StorageTankId,
                Direction = MovementDirection.Out,
                MovementDate = submission.EventDate,
                QuantityMt = inventoryLossMt,
                ReferenceDocument = submission.Reference ?? $"LOSS-{submission.EventDate:yyyyMMdd}",
                Notes = BuildInventoryNotes(submission.Stage, submission.Notes)
            };

            await _stock.EnsureSufficientStockForMovementAsync(movement, ct);
        }

        var lossEvent = new LossEvent
        {
            Stage = submission.Stage,
            ProductId = submission.ProductId,
            ContractId = submission.ContractId,
            ShipmentId = submission.ShipmentId,
            LoadingRegisterId = submission.LoadingRegisterId,
            LoadingReceiptId = submission.LoadingReceiptId,
            TruckDispatchId = submission.TruckDispatchId,
            SalesTransactionId = submission.SalesTransactionId,
            TerminalId = submission.TerminalId,
            StorageTankId = submission.StorageTankId,
            EventDate = submission.EventDate,
            ExpectedQuantityMt = submission.ExpectedQuantityMt,
            ActualQuantityMt = submission.ActualQuantityMt,
            DifferenceQuantityMt = metrics.DifferenceQuantityMt,
            ToleranceQuantityMt = submission.ToleranceQuantityMt,
            AllowableLossMt = metrics.AllowableLossMt,
            ChargeableLossMt = metrics.ChargeableLossMt,
            ResponsiblePartyType = submission.ResponsiblePartyType,
            ResponsiblePartyName = submission.ResponsiblePartyName,
            FinancialTreatment = submission.FinancialTreatment,
            AffectsInventory = submission.AffectsInventory,
            LossCertainty = submission.LossCertainty,
            Reference = submission.Reference,
            Notes = submission.Notes
        };

        _db.LossEvents.Add(lossEvent);
        await _db.SaveChangesAsync(ct);

        if (movement is not null)
        {
            movement.Notes = BuildInventoryNotes(
                submission.Stage,
                $"LossEventId={lossEvent.Id}" + (string.IsNullOrWhiteSpace(submission.Notes) ? string.Empty : $" | {submission.Notes}"));
            _db.InventoryMovements.Add(movement);
            await _db.SaveChangesAsync(ct);

            lossEvent.InventoryMovementId = movement.Id;
            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync(
            nameof(LossEvent),
            lossEvent.Id,
            AuditAction.Insert,
            diff: AuditDiffFormatter.ForCreate(
                ("Stage", lossEvent.Stage),
                ("ProductId", lossEvent.ProductId),
                ("ContractId", lossEvent.ContractId),
                ("ShipmentId", lossEvent.ShipmentId),
                ("LoadingRegisterId", lossEvent.LoadingRegisterId),
                ("LoadingReceiptId", lossEvent.LoadingReceiptId),
                ("TruckDispatchId", lossEvent.TruckDispatchId),
                ("SalesTransactionId", lossEvent.SalesTransactionId),
                ("TerminalId", lossEvent.TerminalId),
                ("StorageTankId", lossEvent.StorageTankId),
                ("EventDate", lossEvent.EventDate),
                ("ExpectedQuantityMt", lossEvent.ExpectedQuantityMt),
                ("ActualQuantityMt", lossEvent.ActualQuantityMt),
                ("DifferenceQuantityMt", lossEvent.DifferenceQuantityMt),
                ("ToleranceQuantityMt", lossEvent.ToleranceQuantityMt),
                ("AllowableLossMt", lossEvent.AllowableLossMt),
                ("ChargeableLossMt", lossEvent.ChargeableLossMt),
                ("AffectsInventory", lossEvent.AffectsInventory),
                ("LossCertainty", lossEvent.LossCertainty),
                ("InventoryMovementId", lossEvent.InventoryMovementId),
                ("Reference", lossEvent.Reference)));

        if (movement is not null)
        {
            await _audit.LogAsync(
                nameof(InventoryMovement),
                movement.Id,
                AuditAction.Insert,
                diff: AuditDiffFormatter.ForCreate(
                    ("ProductId", movement.ProductId),
                    ("ContractId", movement.ContractId),
                    ("TerminalId", movement.TerminalId),
                    ("StorageTankId", movement.StorageTankId),
                    ("Direction", movement.Direction),
                    ("QuantityMt", movement.QuantityMt),
                    ("MovementDate", movement.MovementDate),
                    ("ReferenceDocument", movement.ReferenceDocument),
                    ("LossEventStage", submission.Stage)));
        }

        await _db.SaveChangesAsync(ct);

        if (_lossAccounting is not null)
        {
            await _lossAccounting.TryPostLossAsync(lossEvent, ct);
        }

        return new LossEventWorkflowResult(lossEvent, movement, metrics);
    }

    private static bool CanAffectInventory(LossEventStage stage)
        => stage == LossEventStage.TankNaturalLoss
            || stage == LossEventStage.ManualAdjustment
            || stage == LossEventStage.TankFinalSettlement;

    private static void NormalizeSubmission(LossEventSubmission submission)
    {
        submission.Reference = TrimToNull(submission.Reference);
        submission.Notes = TrimToNull(submission.Notes);
        submission.ResponsiblePartyType = TrimToNull(submission.ResponsiblePartyType);
        submission.ResponsiblePartyName = TrimToNull(submission.ResponsiblePartyName);
        submission.FinancialTreatment = TrimToNull(submission.FinancialTreatment);
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildInventoryNotes(LossEventStage stage, string? notes)
    {
        var prefix = $"LossEvent trace | Stage={stage}";
        if (string.IsNullOrWhiteSpace(notes))
        {
            return prefix;
        }

        var combined = $"{prefix} | {notes.Trim()}";
        return combined.Length <= 1000 ? combined : combined[..1000];
    }
}
