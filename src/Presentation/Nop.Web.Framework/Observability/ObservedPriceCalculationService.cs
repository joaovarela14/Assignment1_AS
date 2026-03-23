using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Discounts;
using Nop.Core.Domain.Stores;
using Nop.Core.Observability;
using Nop.Services.Catalog;

namespace Nop.Web.Framework.Observability;

/// <summary>
/// Decorates the price calculation service to expose latency metrics without changing its business logic.
/// </summary>
public sealed class ObservedPriceCalculationService : IPriceCalculationService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPriceCalculationService _inner;

    public ObservedPriceCalculationService(IPriceCalculationService inner, IHttpContextAccessor httpContextAccessor)
    {
        _inner = inner;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<(decimal priceWithoutDiscounts, decimal finalPrice, decimal appliedDiscountAmount, List<Discount> appliedDiscounts)> GetFinalPriceAsync(
        Product product,
        Customer customer,
        Store store,
        decimal additionalCharge = 0,
        bool includeDiscounts = true,
        int quantity = 1)
    {
        return MeasureAsync(
            "GetFinalPriceAsync.basic",
            () => _inner.GetFinalPriceAsync(product, customer, store, additionalCharge, includeDiscounts, quantity));
    }

    public Task<(decimal priceWithoutDiscounts, decimal finalPrice, decimal appliedDiscountAmount, List<Discount> appliedDiscounts)> GetFinalPriceAsync(
        Product product,
        Customer customer,
        Store store,
        decimal additionalCharge,
        bool includeDiscounts,
        int quantity,
        DateTime? rentalStartDate,
        DateTime? rentalEndDate)
    {
        return MeasureAsync(
            "GetFinalPriceAsync.rental",
            () => _inner.GetFinalPriceAsync(product, customer, store, additionalCharge, includeDiscounts, quantity, rentalStartDate, rentalEndDate));
    }

    public Task<(decimal priceWithoutDiscounts, decimal finalPrice, decimal appliedDiscountAmount, List<Discount> appliedDiscounts)> GetFinalPriceAsync(
        Product product,
        Customer customer,
        Store store,
        decimal? overriddenProductPrice,
        decimal additionalCharge,
        bool includeDiscounts,
        int quantity,
        DateTime? rentalStartDate,
        DateTime? rentalEndDate)
    {
        return MeasureAsync(
            "GetFinalPriceAsync.override",
            () => _inner.GetFinalPriceAsync(product, customer, store, overriddenProductPrice, additionalCharge, includeDiscounts, quantity, rentalStartDate, rentalEndDate));
    }

    public Task<decimal> GetProductCostAsync(Product product, string attributesXml)
    {
        return MeasureAsync("GetProductCostAsync", () => _inner.GetProductCostAsync(product, attributesXml));
    }

    public Task<decimal> GetProductAttributeValuePriceAdjustmentAsync(
        Product product,
        ProductAttributeValue value,
        Customer customer,
        Store store,
        decimal? productPrice = null,
        int quantity = 1)
    {
        return MeasureAsync(
            "GetProductAttributeValuePriceAdjustmentAsync",
            () => _inner.GetProductAttributeValuePriceAdjustmentAsync(product, value, customer, store, productPrice, quantity));
    }

    public Task<decimal> RoundPriceAsync(decimal value, Currency currency = null)
    {
        return MeasureAsync("RoundPriceAsync", () => _inner.RoundPriceAsync(value, currency));
    }

    public decimal Round(decimal value, RoundingType roundingType)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return _inner.Round(value, roundingType);
        }
        finally
        {
            stopwatch.Stop();

            if (IsProductViewRequest())
                NopTelemetry.RecordProductViewPricingLatency("Round", stopwatch.Elapsed.TotalSeconds);
        }
    }

    private async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> action)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            return await action();
        }
        finally
        {
            stopwatch.Stop();

            if (IsProductViewRequest())
                NopTelemetry.RecordProductViewPricingLatency(operation, stopwatch.Elapsed.TotalSeconds);
        }
    }

    private bool IsProductViewRequest()
    {
        var routeValues = _httpContextAccessor.HttpContext?.Request.RouteValues;
        if (routeValues == null)
            return false;

        var controller = routeValues["controller"]?.ToString();
        var action = routeValues["action"]?.ToString();

        return string.Equals(controller, "Product", StringComparison.OrdinalIgnoreCase)
               && string.Equals(action, "ProductDetails", StringComparison.OrdinalIgnoreCase);
    }
}
