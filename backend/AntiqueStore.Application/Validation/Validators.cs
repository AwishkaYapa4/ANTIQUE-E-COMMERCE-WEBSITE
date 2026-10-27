using AntiqueStore.Application.Admin;
using AntiqueStore.Application.Carts;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Content;
using AntiqueStore.Application.Customization;
using AntiqueStore.Application.Engagement;
using AntiqueStore.Application.Media;
using AntiqueStore.Application.Orders;
using AntiqueStore.Application.Users;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Domain.Rules;
using FluentValidation;

namespace AntiqueStore.Application.Validation;

internal static class Rules
{
    public const string PhonePattern = @"^\+?[0-9 ()\-]{7,20}$";

    public static IRuleBuilderOptions<T, string?> SafeLink<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => string.IsNullOrWhiteSpace(v) || Text.IsSafeLink(v))
            .WithMessage("Use a site-relative path (e.g. /shop) or an http(s) URL.");

    public static IRuleBuilderOptions<T, string?> SafeImage<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(v => string.IsNullOrWhiteSpace(v) || Text.IsSafeImageUrl(v))
            .WithMessage("Image URL must be an http(s) or site-relative URL.");

    public static IRuleBuilderOptions<T, string> HexColor<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(Text.IsHexColor).WithMessage("Use a hex color such as #B08D57.");

    public static IRuleBuilderOptions<T, string?> District<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(d => d is not null && SriLanka.Districts.Contains(d)).WithMessage("Select a valid district.");
}

public sealed class AddCartItemValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 20);
    }
}

public sealed class UpdateCartItemValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemValidator() => RuleFor(x => x.Quantity).InclusiveBetween(0, 20);
}

public sealed class MergeCartValidator : AbstractValidator<MergeCartRequest>
{
    public MergeCartValidator()
    {
        RuleFor(x => x.Items).NotNull().Must(i => i.Count <= 50).WithMessage("Too many items.");
        RuleForEach(x => x.Items).SetValidator(new AddCartItemValidator());
    }
}

public sealed class CheckoutQuoteValidator : AbstractValidator<CheckoutQuoteRequest>
{
    public CheckoutQuoteValidator()
    {
        RuleFor(x => x.FulfillmentMethod).IsInEnum();
        RuleFor(x => x.DiscountCode).MaximumLength(64);
        RuleFor(x => x.District).District().When(x => x.FulfillmentMethod == FulfillmentMethod.Delivery && x.District is not null);
    }
}

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().Length(16, 64).Matches("^[A-Za-z0-9_-]+$");
        RuleFor(x => x.CheckoutToken).MaximumLength(64);
        RuleFor(x => x.Contact).NotNull();
        RuleFor(x => x.Contact.FullName).NotEmpty().MaximumLength(200).When(x => x.Contact is not null);
        RuleFor(x => x.Contact.Email).NotEmpty().EmailAddress().MaximumLength(254).When(x => x.Contact is not null);
        RuleFor(x => x.Contact.Phone).NotEmpty().Matches(Rules.PhonePattern).WithMessage("Enter a valid phone number.").When(x => x.Contact is not null);
        RuleFor(x => x.FulfillmentMethod).IsInEnum();
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.Address).NotNull().WithMessage("Delivery address is required.").When(x => x.FulfillmentMethod == FulfillmentMethod.Delivery);
        When(x => x.FulfillmentMethod == FulfillmentMethod.Delivery && x.Address is not null, () =>
        {
            RuleFor(x => x.Address!.Line1).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Address!.Line2).MaximumLength(200);
            RuleFor(x => x.Address!.City).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Address!.District).District();
            RuleFor(x => x.Address!.PostalCode).MaximumLength(20).Matches("^[0-9A-Za-z -]*$");
        });
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.DiscountCode).MaximumLength(64);
        RuleFor(x => x.ExpectedTotal).GreaterThanOrEqualTo(0).When(x => x.ExpectedTotal.HasValue);
    }
}

public sealed class SubmitPaymentReferenceValidator : AbstractValidator<SubmitPaymentReferenceRequest>
{
    public SubmitPaymentReferenceValidator()
    {
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class CancelOrderValidator : AbstractValidator<CancelOrderRequest>
{
    public CancelOrderValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

public sealed class UpdateProfileValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileValidator()
    {
        RuleFor(x => x.DisplayName).MaximumLength(200);
        RuleFor(x => x.PhoneNumber).Matches(Rules.PhonePattern).When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber))
            .WithMessage("Enter a valid phone number.");
    }
}

public sealed class AddressValidator : AbstractValidator<AddressRequest>
{
    public AddressValidator()
    {
        RuleFor(x => x.Label).MaximumLength(64);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).NotEmpty().Matches(Rules.PhonePattern).WithMessage("Enter a valid phone number.");
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Line2).MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.District).District();
        RuleFor(x => x.PostalCode).MaximumLength(20);
    }
}

public sealed class ProductUpsertValidator : AbstractValidator<ProductUpsertRequest>
{
    public ProductUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64).Matches("^[A-Za-z0-9._-]+$").WithMessage("SKU may contain letters, numbers, dots, dashes and underscores.");
        RuleFor(x => x.ShortDescription).MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(20000);
        RuleFor(x => x.Price).GreaterThan(0).LessThan(1_000_000_000);
        RuleFor(x => x.CompareAtPrice).GreaterThan(x => x.Price).When(x => x.CompareAtPrice is > 0)
            .WithMessage("Compare-at price must be higher than the selling price.");
        RuleFor(x => x.Condition).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ConditionNotes).MaximumLength(2000);
        RuleFor(x => x.SignsOfWear).MaximumLength(2000);
        RuleFor(x => x.Scratches).MaximumLength(2000);
        RuleFor(x => x.Damage).MaximumLength(2000);
        RuleFor(x => x.RestorationHistory).MaximumLength(2000);
        RuleFor(x => x.MissingParts).MaximumLength(2000);
        RuleFor(x => x.FunctionalStatus).MaximumLength(2000);
        RuleFor(x => x.Provenance).MaximumLength(2000);
        RuleFor(x => x.ProvenanceEvidence).MaximumLength(2000);
        RuleFor(x => x.Material).MaximumLength(100);
        RuleFor(x => x.Color).MaximumLength(100);
        RuleFor(x => x.Dimensions).MaximumLength(200);
        RuleFor(x => x.Era).MaximumLength(100);
        RuleFor(x => x.ApproximateAge).MaximumLength(100);
        RuleFor(x => x.WeightKg).InclusiveBetween(0, 10000).When(x => x.WeightKg.HasValue);
        RuleFor(x => x.InitialQuantity).InclusiveBetween(0, 10000);
        RuleFor(x => x.MaxPerOrder).InclusiveBetween(1, 20);
        RuleFor(x => x.ProvenanceEvidence).NotEmpty().When(x => x.HasProvenanceEvidence)
            .WithMessage("Describe the documentation you hold before marking provenance as documented.");
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(500);
        RuleFor(x => x.Images).Must(i => i.Count <= 20).WithMessage("A product can have at most 20 images.");
        RuleForEach(x => x.Images).ChildRules(i => i.RuleFor(x => x.AltText).MaximumLength(300));
        RuleFor(x => x.Attributes).Must(a => a.Count <= 30).WithMessage("At most 30 attributes.");
        RuleForEach(x => x.Attributes).ChildRules(a =>
        {
            a.RuleFor(x => x.Name).MaximumLength(100);
            a.RuleFor(x => x.Value).MaximumLength(500);
        });
        RuleFor(x => x.CollectionIds).Must(c => c.Count <= 20);
    }
}

public sealed class AdjustStockValidator : AbstractValidator<AdjustStockRequest>
{
    public AdjustStockValidator()
    {
        RuleFor(x => x.QuantityAvailable).InclusiveBetween(0, 10000);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Give a reason for the stock correction.").MaximumLength(500);
    }
}

public sealed class CategoryUpsertValidator : AbstractValidator<CategoryUpsertRequest>
{
    public CategoryUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ImageUrl).SafeImage();
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(500);
    }
}

public sealed class CollectionUpsertValidator : AbstractValidator<CollectionUpsertRequest>
{
    public CollectionUpsertValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.ImageUrl).SafeImage();
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(500);
    }
}

public sealed class UpdateOrderStatusValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.TrackingCarrier).MaximumLength(100);
        RuleFor(x => x.TrackingNumber).MaximumLength(100);
        RuleFor(x => x.TrackingUrl).SafeLink().MaximumLength(1024);
    }
}

public sealed class UpdatePaymentStatusValidator : AbstractValidator<UpdatePaymentStatusRequest>
{
    public UpdatePaymentStatusValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Reference).MaximumLength(200);
    }
}

public sealed class RecordRefundValidator : AbstractValidator<RecordRefundRequest>
{
    public RecordRefundValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Reference).MaximumLength(200);
    }
}

public sealed class UpdateOrderDetailsValidator : AbstractValidator<UpdateOrderDetailsRequest>
{
    public UpdateOrderDetailsValidator()
    {
        RuleFor(x => x.AdminNotes).MaximumLength(2000);
        RuleFor(x => x.TrackingCarrier).MaximumLength(100);
        RuleFor(x => x.TrackingNumber).MaximumLength(100);
        RuleFor(x => x.TrackingUrl).SafeLink().MaximumLength(1024);
    }
}

public sealed class BrandSettingsValidator : AbstractValidator<BrandSettingsDto>
{
    public BrandSettingsValidator()
    {
        RuleFor(x => x.StoreName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Tagline).MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.LogoUrl).SafeImage();
        RuleFor(x => x.FaviconUrl).SafeImage();
        RuleFor(x => x.DefaultOgImageUrl).SafeImage();
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.ContactPhone).Matches(Rules.PhonePattern).When(x => !string.IsNullOrWhiteSpace(x.ContactPhone));
        RuleFor(x => x.WhatsAppNumber).Matches(Rules.PhonePattern).When(x => !string.IsNullOrWhiteSpace(x.WhatsAppNumber));
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.OpeningHours).MaximumLength(500);
        RuleFor(x => x.MapEmbedUrl).Must(u => string.IsNullOrWhiteSpace(u) || u.StartsWith("https://www.google.com/maps/embed", StringComparison.Ordinal))
            .WithMessage("Use a Google Maps embed URL (https://www.google.com/maps/embed?...).");
        RuleFor(x => x.SocialLinks).Must(l => l.Count <= 10);
        RuleForEach(x => x.SocialLinks).ChildRules(l =>
        {
            l.RuleFor(x => x.Platform).NotEmpty().MaximumLength(30);
            l.RuleFor(x => x.Url).NotEmpty().Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                .WithMessage("Social links must be https URLs.");
        });
        RuleFor(x => x.AnnouncementText).MaximumLength(300);
        RuleFor(x => x.AnnouncementLink).SafeLink();
        RuleFor(x => x.FooterDescription).MaximumLength(2000);
        RuleFor(x => x.CopyrightText).MaximumLength(300);
        RuleFor(x => x.SeoDefaultTitle).MaximumLength(200);
        RuleFor(x => x.SeoDefaultDescription).MaximumLength(500);
    }
}

public sealed class ThemeValidator : AbstractValidator<ThemeDto>
{
    public ThemeValidator()
    {
        RuleFor(x => x.BackgroundColor).HexColor();
        RuleFor(x => x.SurfaceColor).HexColor();
        RuleFor(x => x.TextColor).HexColor();
        RuleFor(x => x.MutedTextColor).HexColor();
        RuleFor(x => x.PrimaryColor).HexColor();
        RuleFor(x => x.AccentColor).HexColor();
        RuleFor(x => x.SecondaryColor).HexColor();
        RuleFor(x => x.BorderColor).HexColor();
        RuleFor(x => x.ButtonColor).HexColor();
        RuleFor(x => x.ButtonTextColor).HexColor();
        RuleFor(x => x.SuccessColor).HexColor();
        RuleFor(x => x.ErrorColor).HexColor();
        RuleFor(x => x.HeadingFont).Must(FontCatalog.Heading.Contains).WithMessage("Choose one of the approved heading fonts.");
        RuleFor(x => x.BodyFont).Must(FontCatalog.Body.Contains).WithMessage("Choose one of the approved body fonts.");
        RuleFor(x => x.BaseFontSize).InclusiveBetween(14, 20);
        RuleFor(x => x.HeadingScale).InclusiveBetween(110, 150);
        RuleFor(x => x.BorderRadius).InclusiveBetween(0, 16);
    }
}

public sealed class SaveNavigationValidator : AbstractValidator<SaveNavigationRequest>
{
    public SaveNavigationValidator()
    {
        RuleFor(x => x.Menus).NotNull();
        RuleForEach(x => x.Menus).ChildRules(m =>
        {
            m.RuleFor(x => x.Location).Must(NavigationLocations.All.Contains).WithMessage("Unknown menu location.");
            m.RuleFor(x => x.Title).MaximumLength(200);
            m.RuleFor(x => x.Items).Must(i => i.Count <= 12).WithMessage("A menu can have at most 12 items.");
            m.RuleForEach(x => x.Items).SetValidator(new NavItemValidator());
        });
    }
}

public sealed class NavItemValidator : AbstractValidator<NavItem>
{
    /// <summary>Top-level item validator (also what DI resolves when validators are auto-registered).</summary>
    public NavItemValidator() : this(0) { }

    private NavItemValidator(int depth)
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Href).NotEmpty().Must(Text.IsSafeLink).WithMessage("Use a site-relative path (e.g. /shop) or an http(s) URL.");
        if (depth == 0)
        {
            RuleFor(x => x.Children).Must(c => c is null || c.Count <= 12);
            RuleForEach(x => x.Children).SetValidator(new NavItemValidator(depth + 1));
        }
        else
        {
            RuleFor(x => x.Children).Must(c => c is null || c.Count == 0).WithMessage("Menus support one level of sub-items.");
        }
    }
}

public sealed class SaveHomepageValidator : AbstractValidator<SaveHomepageRequest>
{
    public SaveHomepageValidator()
    {
        RuleFor(x => x.Sections).NotNull();
        RuleForEach(x => x.Sections).ChildRules(s =>
        {
            s.RuleFor(x => x.Type).Must(SectionTypes.All.Contains).WithMessage("Unknown section type.");
            s.RuleFor(x => x.Key).MaximumLength(64).Matches("^[A-Za-z0-9_-]*$");
        });
    }
}

public sealed class ContentPageValidator : AbstractValidator<ContentPageUpsertRequest>
{
    public ContentPageValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(200);
        RuleFor(x => x.Body).NotNull().MaximumLength(100_000);
        RuleFor(x => x.SeoTitle).MaximumLength(200);
        RuleFor(x => x.SeoDescription).MaximumLength(500);
    }
}

public sealed class EnquiryValidator : AbstractValidator<EnquiryRequest>
{
    public EnquiryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Phone).Matches(Rules.PhonePattern).When(x => !string.IsNullOrWhiteSpace(x.Phone)).WithMessage("Enter a valid phone number.");
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MinimumLength(10).MaximumLength(4000);
        RuleFor(x => x.Website).MaximumLength(200);
    }
}

public sealed class UpdateEnquiryValidator : AbstractValidator<UpdateEnquiryRequest>
{
    public UpdateEnquiryValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.AdminNotes).MaximumLength(2000);
    }
}

public sealed class NewsletterValidator : AbstractValidator<NewsletterRequest>
{
    public NewsletterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Source).MaximumLength(64);
    }
}

public sealed class TestimonialValidator : AbstractValidator<TestimonialUpsertRequest>
{
    public TestimonialValidator()
    {
        RuleFor(x => x.Quote).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AuthorLocation).MaximumLength(200);
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).When(x => x.Rating.HasValue);
    }
}

public sealed class CreateReviewValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Title).MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MinimumLength(10).MaximumLength(2000);
    }
}

public sealed class DiscountCodeValidator : AbstractValidator<DiscountCodeUpsertRequest>
{
    public DiscountCodeValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32).Matches("^[A-Za-z0-9_-]+$").WithMessage("Use letters, numbers, dashes or underscores.");
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(100).When(x => x.Type == DiscountType.Percentage).WithMessage("Percentage must be at most 100.");
        RuleFor(x => x.MinOrderSubtotal).GreaterThanOrEqualTo(0).When(x => x.MinOrderSubtotal.HasValue);
        RuleFor(x => x.MaxDiscountAmount).GreaterThan(0).When(x => x.MaxDiscountAmount.HasValue);
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt).When(x => x.StartsAt.HasValue && x.EndsAt.HasValue)
            .WithMessage("End date must be after the start date.");
        RuleFor(x => x.UsageLimit).GreaterThan(0).When(x => x.UsageLimit.HasValue);
        RuleFor(x => x.UsageLimitPerCustomer).GreaterThan(0).When(x => x.UsageLimitPerCustomer.HasValue);
    }
}

public sealed class ShippingZoneValidator : AbstractValidator<ShippingZoneDto>
{
    public ShippingZoneValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.EstimatedDelivery).MaximumLength(200);
        RuleFor(x => x.Districts).NotNull();
        RuleFor(x => x.Rates).NotNull().Must(r => r.Count <= 20);
        RuleForEach(x => x.Rates).ChildRules(r =>
        {
            r.RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            r.RuleFor(x => x.Fee).GreaterThanOrEqualTo(0).LessThan(1_000_000);
            r.RuleFor(x => x.MaxSubtotal).GreaterThan(x => x.MinSubtotal ?? 0).When(x => x.MaxSubtotal.HasValue);
        });
    }
}

public sealed class CommerceSettingsValidator : AbstractValidator<CommerceSettingsDto>
{
    public CommerceSettingsValidator()
    {
        RuleFor(x => x.OrderNumberPrefix).NotEmpty().MaximumLength(8).Matches("^[A-Za-z]+$");
        RuleFor(x => x.ReservationMinutes).InclusiveBetween(5, 120);
        RuleFor(x => x.BankTransferPaymentWindowHours).InclusiveBetween(0, 24 * 14);
        RuleFor(x => x.FreeShippingThreshold).GreaterThan(0).When(x => x.FreeShippingThreshold.HasValue);
        RuleFor(x => x.CashOnDeliveryMaxOrderValue).GreaterThan(0).When(x => x.CashOnDeliveryMaxOrderValue.HasValue);
        RuleFor(x => x.LocalPickupAddress).NotEmpty().When(x => x.LocalPickupEnabled).WithMessage("Enter the pickup address.");
        RuleFor(x => x.BankAccountNumber).NotEmpty().When(x => x.BankTransferEnabled).WithMessage("Enter bank account details to offer bank transfer.");
        RuleFor(x => x.BankAccountNumber).MaximumLength(64);
        RuleFor(x => x.BankName).MaximumLength(200);
        RuleFor(x => x.BankAccountName).MaximumLength(200);
        RuleFor(x => x.BankBranch).MaximumLength(200);
        RuleFor(x => x.BankTransferInstructions).MaximumLength(2000);
        RuleFor(x => x.DeliveryInstructions).MaximumLength(2000);
        RuleFor(x => x.LocalPickupInstructions).MaximumLength(2000);
        RuleFor(x => x.ReturnsSummary).MaximumLength(2000);
    }
}

public sealed class UpdateMediaValidator : AbstractValidator<UpdateMediaRequest>
{
    public UpdateMediaValidator()
    {
        RuleFor(x => x.AltText).MaximumLength(300);
        RuleFor(x => x.Title).MaximumLength(200);
    }
}
