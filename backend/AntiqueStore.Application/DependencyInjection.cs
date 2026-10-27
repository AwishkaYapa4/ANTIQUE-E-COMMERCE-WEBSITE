using AntiqueStore.Application.Admin;
using AntiqueStore.Application.Carts;
using AntiqueStore.Application.Catalog;
using AntiqueStore.Application.Content;
using AntiqueStore.Application.Customization;
using AntiqueStore.Application.Engagement;
using AntiqueStore.Application.Inventory;
using AntiqueStore.Application.Media;
using AntiqueStore.Application.Orders;
using AntiqueStore.Application.Payments;
using AntiqueStore.Application.Promotions;
using AntiqueStore.Application.Reports;
using AntiqueStore.Application.Settings;
using AntiqueStore.Application.Shipping;
using AntiqueStore.Application.Users;
using AntiqueStore.Application.Wishlists;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AntiqueStore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CatalogService>(ServiceLifetime.Singleton, includeInternalTypes: true);
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<CatalogService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<CartService>();
        services.AddScoped<WishlistService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<ShippingService>();
        services.AddScoped<DiscountService>();
        services.AddScoped<CommerceSettingsService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<OrderLifecycle>();
        services.AddScoped<OrderQueries>();
        services.AddScoped<CustomerOrderService>();
        services.AddScoped<AdminOrderService>();
        services.AddScoped<OrderMaintenanceService>();
        services.AddScoped<AdminProductService>();
        services.AddScoped<AdminTaxonomyService>();
        services.AddScoped<AdminDiscountService>();
        services.AddScoped<AdminShippingService>();
        services.AddScoped<AdminSettingsService>();
        services.AddScoped<AdminCustomerService>();
        services.AddScoped<AuditLogQueryService>();
        services.AddScoped<CustomizationService>();
        services.AddScoped<ContentPageService>();
        services.AddScoped<MediaService>();
        services.AddScoped<EnquiryService>();
        services.AddScoped<NewsletterService>();
        services.AddScoped<TestimonialService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<ReportingService>();

        services.AddSingleton<IPaymentMethodProvider, CashOnDeliveryProvider>();
        services.AddSingleton<IPaymentMethodProvider, BankTransferProvider>();
        services.AddSingleton<PaymentMethodRegistry>();
        return services;
    }
}
