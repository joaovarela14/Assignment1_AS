using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Nop.Core.Observability;

/// <summary>
/// Central telemetry primitives shared across nopCommerce layers.
/// </summary>
public static class NopTelemetry
{
    public const string ActivitySourceName = "NopCommerce.Observability";
    public const string MeterName = "NopCommerce.Observability";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, NopVersion.FULL_VERSION);
    public static readonly Meter Meter = new(MeterName, NopVersion.FULL_VERSION);

    private static readonly Counter<long> _catalogSearchZeroResultsCounter =
        Meter.CreateCounter<long>(
            "nopcommerce_catalog_search_zero_results_total",
            description: "Number of catalog searches that returned no products.");

    private static readonly Histogram<double> _productViewPricingLatencyHistogram =
        Meter.CreateHistogram<double>(
            "nopcommerce_product_view_pricing_latency_seconds",
            unit: "s",
            description: "Time spent calculating prices and discounts during a product view.");

    public static void RecordCatalogSearchZeroResults(bool advancedSearch, int pageNumber, int pageSize)
    {
        var tags = new TagList
        {
            { "catalog.search.advanced", advancedSearch },
            { "catalog.search.page_number", pageNumber },
            { "catalog.search.page_size", pageSize }
        };

        _catalogSearchZeroResultsCounter.Add(1, tags);
    }

    public static void RecordProductViewPricingLatency(string operation, double durationSeconds)
    {
        var tags = new TagList
        {
            { "pricing.operation", operation }
        };

        _productViewPricingLatencyHistogram.Record(durationSeconds, tags);
    }
}
