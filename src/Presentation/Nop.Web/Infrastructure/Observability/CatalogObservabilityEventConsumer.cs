using System.Diagnostics;
using Nop.Core.Observability;
using Nop.Services.Events;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Models;
using Nop.Web.Models.Catalog;

namespace Nop.Web.Infrastructure.Observability;

/// <summary>
/// Hooks product-view telemetry into the existing nopCommerce event pipeline.
/// </summary>
public sealed class CatalogObservabilityEventConsumer : IConsumer<ModelPreparedEvent<BaseNopModel>>
{
    public Task HandleEventAsync(ModelPreparedEvent<BaseNopModel> eventMessage)
    {
        if (eventMessage.Model is not ProductDetailsModel productDetailsModel)
            return Task.CompletedTask;

        using var activity = NopTelemetry.ActivitySource.StartActivity("catalog.product.view", ActivityKind.Internal);
        activity?.SetTag("catalog.product.id", productDetailsModel.Id);
        activity?.SetTag("catalog.product.associated_products_count", productDetailsModel.AssociatedProducts.Count);
        activity?.SetTag("catalog.product.visible_individually", productDetailsModel.VisibleIndividually);

        return Task.CompletedTask;
    }
}
