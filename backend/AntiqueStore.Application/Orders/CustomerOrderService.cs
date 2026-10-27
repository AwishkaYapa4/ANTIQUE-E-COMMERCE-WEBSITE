using AntiqueStore.Application.Common;
using AntiqueStore.Domain.Entities;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace AntiqueStore.Application.Orders;

public sealed record UploadedFile(Stream Content, string FileName, string ContentType, long Length);

/// <summary>Actions a customer can take on their own orders.</summary>
public sealed class CustomerOrderService(
    IAppDbContext db, OrderLifecycle lifecycle, OrderQueries queries, IPrivateFileStorage privateFiles)
{
    public const long MaxProofBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> AllowedProofTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg", ["image/png"] = ".png", ["image/webp"] = ".webp", ["application/pdf"] = ".pdf"
    };

    private async Task<Order> LoadOwnedAsync(int userId, int orderId, CancellationToken ct) =>
        await db.Orders.Include(o => o.Items).Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, ct) ?? throw new NotFoundException("Order", orderId);

    public async Task<OrderDto> CancelAsync(int userId, int orderId, CancelOrderRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await LoadOwnedAsync(userId, orderId, ct);
        if (!OrderRules.CustomerCanCancel(order.Status, order.PaymentStatus))
            throw new BusinessRuleException("This order can no longer be cancelled online. Please contact us.", "cannot_cancel");

        await lifecycle.CancelAsync(order, Text.TrimToNull(request.Reason) ?? "Cancelled by customer", userId, restock: true, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await queries.GetForCustomerAsync(userId, orderId, ct);
    }

    /// <summary>Customer reports a bank transfer. The order is NOT marked paid until an admin verifies it.</summary>
    public async Task<OrderDto> SubmitBankTransferAsync(int userId, int orderId, SubmitPaymentReferenceRequest request,
        UploadedFile? proof, CancellationToken ct)
    {
        if (proof is not null) ValidateProof(proof);

        var order = await LoadOwnedAsync(userId, orderId, ct);
        if (order.PaymentMethod != PaymentMethod.BankTransfer)
            throw new BusinessRuleException("Payment details can only be submitted for bank transfer orders.", "not_bank_transfer");
        if (order.Status == OrderStatus.Cancelled)
            throw new BusinessRuleException("This order has been cancelled.", "order_cancelled");
        if (order.PaymentStatus is not (PaymentStatus.Unpaid or PaymentStatus.Failed))
            throw new BusinessRuleException("Payment for this order has already been submitted.", "payment_already_submitted");

        var payment = order.Payments.Where(p => p.Kind == PaymentKind.Charge).OrderByDescending(p => p.Id).FirstOrDefault();
        if (payment is null || payment.Status == PaymentRecordStatus.Failed)
        {
            payment = new Payment { Method = PaymentMethod.BankTransfer, Amount = order.Total, Currency = order.Currency, ProviderName = "BankTransfer" };
            order.Payments.Add(payment);
        }
        payment.Reference = request.Reference.Trim();
        payment.Notes = Text.TrimToNull(request.Notes);
        payment.Status = PaymentRecordStatus.Pending;

        string? storedKey = null;
        if (proof is not null)
        {
            storedKey = await privateFiles.SaveAsync(proof.Content, AllowedProofTypes[proof.ContentType], "payment-proofs", ct);
            payment.Proofs.Add(new PaymentProof
            {
                StorageKey = storedKey, OriginalFileName = Path.GetFileName(proof.FileName), ContentType = proof.ContentType,
                SizeBytes = proof.Length, UploadedByUserId = userId
            });
        }

        lifecycle.SetPaymentStatus(order, PaymentStatus.PendingVerification, "Bank transfer details submitted", userId);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            if (storedKey is not null) await privateFiles.DeleteAsync(storedKey, CancellationToken.None);
            throw;
        }
        return await queries.GetForCustomerAsync(userId, orderId, ct);
    }

    private static void ValidateProof(UploadedFile proof)
    {
        if (proof.Length <= 0 || proof.Length > MaxProofBytes)
            throw RequestValidationException.For("proof", "Payment proof must be smaller than 5 MB.");
        if (!AllowedProofTypes.ContainsKey(proof.ContentType))
            throw RequestValidationException.For("proof", "Upload a JPG, PNG, WEBP or PDF file.");
        if (!FileSignatures.Matches(proof.Content, proof.ContentType))
            throw RequestValidationException.For("proof", "The file content does not match its type.");
    }
}

/// <summary>Magic-byte checks so a renamed executable can't be uploaded as an image/PDF.</summary>
public static class FileSignatures
{
    public static bool Matches(Stream stream, string contentType)
    {
        if (!stream.CanSeek) return false;
        Span<byte> header = stackalloc byte[12];
        var start = stream.Position;
        var read = stream.Read(header);
        stream.Position = start;
        if (read < 4) return false;
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            "image/webp" => read >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
            "application/pdf" => header[..4].SequenceEqual("%PDF"u8),
            _ => false
        };
    }
}
