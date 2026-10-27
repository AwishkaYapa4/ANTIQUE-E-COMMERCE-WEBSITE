using AntiqueStore.Api.Auth;
using AntiqueStore.Application.Carts;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Orders;
using AntiqueStore.Application.Users;
using AntiqueStore.Application.Wishlists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AntiqueStore.Api.Controllers;

[ApiController]
[Authorize]
public abstract class CustomerControllerBase(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Always the authenticated caller; user ids are never accepted from the client.</summary>
    protected int UserId => currentUser.RequireUserId();
}

[Route("api/me")]
public sealed class MeController(ProfileService profiles, ICurrentUser currentUser) : CustomerControllerBase(currentUser)
{
    [HttpGet]
    public Task<MeDto> Get(CancellationToken ct) => profiles.GetMeAsync(UserId, ct);

    [HttpPut]
    public Task<MeDto> Update(UpdateProfileRequest request, CancellationToken ct) => profiles.UpdateMeAsync(UserId, request, ct);

    [HttpGet("addresses")]
    public Task<IReadOnlyList<AddressDto>> Addresses(CancellationToken ct) => profiles.GetAddressesAsync(UserId, ct);

    [HttpPost("addresses")]
    public Task<IReadOnlyList<AddressDto>> AddAddress(AddressRequest request, CancellationToken ct) => profiles.AddAddressAsync(UserId, request, ct);

    [HttpPut("addresses/{id:int}")]
    public Task<IReadOnlyList<AddressDto>> UpdateAddress(int id, AddressRequest request, CancellationToken ct) =>
        profiles.UpdateAddressAsync(UserId, id, request, ct);

    [HttpDelete("addresses/{id:int}")]
    public Task<IReadOnlyList<AddressDto>> DeleteAddress(int id, CancellationToken ct) => profiles.DeleteAddressAsync(UserId, id, ct);
}

[Route("api/cart")]
public sealed class CartController(CartService carts, ICurrentUser currentUser) : CustomerControllerBase(currentUser)
{
    [HttpGet]
    public Task<CartDto> Get(CancellationToken ct) => carts.GetAsync(UserId, ct);

    [HttpPost("items")]
    public Task<CartDto> Add(AddCartItemRequest request, CancellationToken ct) => carts.AddAsync(UserId, request, ct);

    [HttpPatch("items/{id:int}")]
    public Task<CartDto> Update(int id, UpdateCartItemRequest request, CancellationToken ct) => carts.UpdateAsync(UserId, id, request, ct);

    [HttpDelete("items/{id:int}")]
    public Task<CartDto> Remove(int id, CancellationToken ct) => carts.RemoveAsync(UserId, id, ct);

    /// <summary>Merges a guest (browser-stored) cart after sign-in.</summary>
    [HttpPost("merge")]
    public Task<MergeCartResult> Merge(MergeCartRequest request, CancellationToken ct) => carts.MergeAsync(UserId, request, ct);
}

[Route("api/wishlist")]
public sealed class WishlistController(WishlistService wishlists, ICurrentUser currentUser) : CustomerControllerBase(currentUser)
{
    [HttpGet]
    public Task<IReadOnlyList<WishlistItemDto>> Get(CancellationToken ct) => wishlists.GetAsync(UserId, ct);

    [HttpPost("items")]
    public Task<IReadOnlyList<WishlistItemDto>> Add(AddWishlistItemRequest request, CancellationToken ct) => wishlists.AddAsync(UserId, request, ct);

    [HttpDelete("items/{id:int}")]
    public Task<IReadOnlyList<WishlistItemDto>> Remove(int id, CancellationToken ct) => wishlists.RemoveAsync(UserId, id, false, ct);

    [HttpDelete("products/{productId:int}")]
    public Task<IReadOnlyList<WishlistItemDto>> RemoveProduct(int productId, CancellationToken ct) => wishlists.RemoveAsync(UserId, productId, true, ct);

    [HttpPost("merge")]
    public Task<IReadOnlyList<WishlistItemDto>> Merge(IReadOnlyList<int> productIds, CancellationToken ct) => wishlists.MergeAsync(UserId, productIds, ct);
}

[Route("api/checkout")]
[EnableRateLimiting("checkout")]
public sealed class CheckoutController(CheckoutService checkout, ICurrentUser currentUser) : CustomerControllerBase(currentUser)
{
    [HttpPost("quote")]
    public Task<CheckoutQuoteDto> Quote(CheckoutQuoteRequest request, CancellationToken ct) => checkout.QuoteAsync(UserId, request, ct);

    /// <summary>Temporarily holds every item in the cart for this customer.</summary>
    [HttpPost("reservations")]
    [Authorize(Policy = AuthConstants.VerifiedEmailPolicy)]
    public Task<CheckoutReservationDto> Reserve(CancellationToken ct) => checkout.ReserveAsync(UserId, ct);

    [HttpDelete("reservations")]
    public async Task<IActionResult> Release(CancellationToken ct)
    {
        await checkout.ReleaseAsync(UserId, ct);
        return NoContent();
    }
}

[Route("api/orders")]
public sealed class OrdersController(CheckoutService checkout, OrderQueries queries, CustomerOrderService orders, ICurrentUser currentUser)
    : CustomerControllerBase(currentUser)
{
    /// <summary>Places an order. Idempotent per (user, idempotencyKey): retries return the original order.</summary>
    [HttpPost]
    [Authorize(Policy = AuthConstants.VerifiedEmailPolicy)]
    [EnableRateLimiting("checkout")]
    public async Task<ActionResult<OrderDto>> Place(PlaceOrderRequest request, CancellationToken ct)
    {
        var result = await checkout.PlaceOrderAsync(UserId, request, ct);
        return result.WasDuplicate ? Ok(result.Order) : StatusCode(201, result.Order);
    }

    [HttpGet("my")]
    public Task<PagedResult<OrderSummaryDto>> Mine([FromQuery] PageRequest page, CancellationToken ct) => queries.ListForCustomerAsync(UserId, page, ct);

    [HttpGet("{id:int}")]
    public Task<OrderDto> Get(int id, CancellationToken ct) => queries.GetForCustomerAsync(UserId, id, ct);

    [HttpGet("number/{orderNumber}")]
    public Task<OrderDto> GetByNumber(string orderNumber, CancellationToken ct) => queries.GetForCustomerByNumberAsync(UserId, orderNumber, ct);

    [HttpPost("{id:int}/cancel")]
    public Task<OrderDto> Cancel(int id, CancelOrderRequest request, CancellationToken ct) => orders.CancelAsync(UserId, id, request, ct);

    /// <summary>Submit bank transfer reference and optional proof (JPG/PNG/WEBP/PDF, max 5 MB).</summary>
    [HttpPost("{id:int}/payment")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [EnableRateLimiting("forms")]
    public async Task<OrderDto> SubmitPayment(int id, [FromForm] string reference, [FromForm] string? notes, IFormFile? proof, CancellationToken ct)
    {
        var request = new SubmitPaymentReferenceRequest(reference ?? "", notes);
        if (string.IsNullOrWhiteSpace(request.Reference) || request.Reference.Length > 200)
            throw RequestValidationException.For("reference", "Enter the bank transfer reference (max 200 characters).");
        if (proof is null) return await orders.SubmitBankTransferAsync(UserId, id, request, null, ct);
        if (proof.Length > CustomerOrderService.MaxProofBytes)
            throw RequestValidationException.For("proof", "Payment proof must be smaller than 5 MB.");

        await using var buffer = new MemoryStream();
        await proof.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return await orders.SubmitBankTransferAsync(UserId, id, request,
            new UploadedFile(buffer, proof.FileName, proof.ContentType, proof.Length), ct);
    }
}
