# Assignment 1: nopCommerce + OpenTelemetry

This repository instruments `nopCommerce` with OpenTelemetry for the flow `Customer searches and views a product`.

The goal was to add useful tracing and metrics with small code changes, while keeping a strict privacy strategy so that search terms, emails, usernames, and other PII are not exported in telemetry.

## What This Repository Contains

- end-to-end tracing for the catalog search and product view flow
- two custom operational metrics
- privacy redaction at the OpenTelemetry SDK layer
- a local observability stack with `Jaeger`, `Prometheus`, and `Grafana`
- a `k6` load test for the selected flow
- an architectural critique in [CRITIQUE.md](docs/CRITIQUE.md)

## Chosen Flow

Selected flow: `Customer searches and views a product`

This flow crosses the parts of nopCommerce that matter for catalog observability:

- HTTP entry in `Nop.Web`
- search preparation in the catalog model factory
- search and product loading in `Nop.Services`
- database access through nopCommerce repositories and ADO.NET providers
- pricing calculation during product view

## Architecture Diagram

Draw.io source:

- [customer-search-and-view.drawio](docs/diagrams/customer-search-and-view.drawio)

## How To Run

### 1. Start the stack

```bash
docker compose up -d --build
```

### 2. Open the observability tools

After installing and starting the stack, these are the relevant URLs:

- Store: `http://localhost`
- Metrics endpoint: `http://localhost/metrics`
- Prometheus: `http://localhost:9090`
- Jaeger: `http://localhost:16686`
- Grafana: `http://localhost:33000`

Grafana credentials:

- username: `admin`
- password: `admin`

## How to view the Dashboard

Open Grafana and load the `nopCommerce Observability` dashboard.

It includes:

- a total panel for empty search results
- a percentage panel for empty search results in the last 5 minutes
- an average panel for pricing latency
- a p95 panel for pricing latency
- three HTTP error percentage panels for the store:
  - `404 Not Found`
  - `Other 4xx`
  - `5xx Server Error`

Each error panel shows the last 5 minutes and turns red above `0.1%`.

Traces are viewed directly in Jaeger at `http://localhost:16686`.

## Architecture Analysis

### How the layers are organised and what the dependency rules are

nopCommerce is organised in a layered way.

- `Nop.Core` is the base layer. It contains domain entities, settings, events, caching abstractions, and core infrastructure.
- `Nop.Data` is the data layer. It contains repositories, mappings, migrations, and provider-specific database support.
- `Nop.Services` is the business layer. It contains the main workflows and domain logic.
- `Nop.Web.Framework` is shared web infrastructure. It contains MVC support code, startup code, filters, routing helpers, and UI helpers.
- `Nop.Web` is the web application for the public store and the admin area.
- `Plugins` extend the system through dependency injection and event consumers.

The dependency direction is mostly bottom to top:

- `Nop.Core` depends on nothing else in nopCommerce
- `Nop.Data` depends on `Nop.Core`
- `Nop.Services` depends on `Nop.Core` and `Nop.Data`
- `Nop.Web.Framework` depends on `Nop.Core`, `Nop.Data`, and `Nop.Services`
- `Nop.Web` depends on all the lower layers

This is a layered architecture, but not a strict clean architecture. The web framework already knows about services and data. That makes extension practical, but it also means the boundaries are not as clean as they look at first.

### How nopCommerce handles events internally

nopCommerce uses an in-process event mechanism.

The main abstraction is `IEventPublisher`. A class publishes an event, and nopCommerce resolves all matching `IConsumer<TEvent>` handlers from dependency injection.

What this means in practice:

- the event stays inside the same process
- handlers are called one after another
- it is an extension mechanism, not a message broker
- failures are logged and the system keeps going

This is useful for observability because it gives natural attachment points without forcing changes into core business logic.

In this work, product-view instrumentation uses that mechanism through [CatalogObservabilityEventConsumer.cs](src/Presentation/Nop.Web/Infrastructure/Observability/CatalogObservabilityEventConsumer.cs).

### Where the code makes observability easy

nopCommerce helps observability in a few important ways.

- The layers are readable enough that a business flow can be followed from controller to service to data access.
- The event system gives a safe extension point for some signals.
- Dependency injection makes it possible to add decorators for cross-cutting concerns.
- HTTP and database activity can be captured with OpenTelemetry instrumentation plus a small amount of manual tracing.

For the selected flow, these were the best boundaries:

- `CatalogController` for the HTTP entry point
- `ProductService` and `CategoryService` for business work
- `IEventPublisher` consumer for product-view events
- `IPriceCalculationService` decorator for pricing latency

### Where the code makes observability hard

The codebase also creates some friction.

- There is no shared observability layer in the original architecture.
- Read-heavy flows have weaker event coverage than write-heavy flows.
- Some code paths still rely on indirect resolution patterns, which makes complete decoration harder.
- Event coverage is not universal, so an event-only strategy would miss parts of the flow.

There is also one important technical detail: this nopCommerce version does not use Entity Framework Core for this catalog path. It uses `linq2db` over ADO.NET providers. Because of that, the correct automatic database tracing here is provider instrumentation such as `SqlClient`, not `AddEntityFrameworkCoreInstrumentation()`.

### What would need to change structurally, and is it worth it

I would not redesign the platform just to add observability.

The changes that are worth making are small and targeted:

- keep one shared `ActivitySource` and `Meter`
- keep privacy redaction in one SDK-layer processor
- add decorators or event consumers at infrastructure boundaries
- keep spans close to entry points and service boundaries, not deep inside business logic

A larger cleanup, like removing all service-location style patterns and fully isolating layers, would improve maintainability. But the cost is high, and it is bigger than an observability task. For this assignment, surgical changes are the right tradeoff.

## Instrumentation Summary

### Distributed tracing

Tracing covers the selected flow from HTTP to service layer to database.

Manual spans were added in these places:

- [CatalogController.cs](src/Presentation/Nop.Web/Controllers/CatalogController.cs) for `catalog.search` and `catalog.search.refresh`
- [ProductService.cs](src/Libraries/Nop.Services/Catalog/ProductService.cs) for `catalog.product.search` and `catalog.product.load`
- [CategoryService.cs](src/Libraries/Nop.Services/Catalog/CategoryService.cs) for `catalog.category.children`
- [CatalogObservabilityEventConsumer.cs](src/Presentation/Nop.Web/Infrastructure/Observability/CatalogObservabilityEventConsumer.cs) for `catalog.product.view`

Automatic tracing is configured in [ObservabilityExtensions.cs](src/Presentation/Nop.Web.Framework/Infrastructure/Extensions/ObservabilityExtensions.cs):

- `AddAspNetCoreInstrumentation()`
- `AddHttpClientInstrumentation()`
- `AddSqlClientInstrumentation()`

Jaeger receives traces through OTLP from the application container.

### Privacy strategy

Privacy is enforced at the OpenTelemetry SDK layer, before export.

The redaction processor is [SensitiveActivityProcessor.cs](src/Presentation/Nop.Web.Framework/Infrastructure/Extensions/SensitiveActivityProcessor.cs).

It removes or masks fields such as:

- `db.statement`
- `db.query.text`
- search query values
- emails
- usernames
- customer identifiers
- payment-related fields

Telemetry log export is opt-in only. By default, traces and metrics are exported, but telemetry logs are not exported unless explicitly enabled.

## Custom Metrics

The assignment asked for operational metrics, not just generic request counts. The two main custom metrics are:

### `nopcommerce_catalog_search_zero_results_total`

Defined in [NopTelemetry.cs](src/Libraries/Nop.Core/Observability/NopTelemetry.cs) and recorded in [CatalogModelFactory.cs](src/Presentation/Nop.Web/Factories/CatalogModelFactory.cs).

What it means:

- it counts how many searches return zero products

Why it matters:

- it can reveal catalog gaps
- it can reveal search/indexing issues
- it can reveal popular user intent that the store cannot currently satisfy

This is useful to operators because it points to a real business problem before it becomes a customer complaint.

### Supporting search volume metric

The dashboard also uses an extra counter:

- `nopcommerce_catalog_search_total`

This counter tracks total catalog searches. It is not the main business signal by itself, but it makes it possible to calculate the percentage of searches with no results in the last 5 minutes.

### `nopcommerce_product_view_pricing_latency_seconds`

Defined in [NopTelemetry.cs](src/Libraries/Nop.Core/Observability/NopTelemetry.cs) and recorded by the decorator [ObservedPriceCalculationService.cs](src/Presentation/Nop.Web.Framework/Observability/ObservedPriceCalculationService.cs).

What it means:

- it measures how long price and discount calculations take during product-page views

Why it matters:

- pricing logic can become slow before the user sees an error
- discount rules and attribute pricing are common sources of hidden latency
- a histogram makes it possible to watch both average duration and p95 duration

This is useful to operators because it isolates one expensive part of the product-view experience instead of hiding it inside total request latency.

## Dashboard And Load Test

Dashboard files:

- [nopcommerce-observability.json](observability/grafana/dashboards/nopcommerce-observability.json)
- [prometheus.yml](observability/prometheus/prometheus.yml)

Load test files:

- [LOAD_TESTING.md](docs/LOAD_TESTING.md)
- [catalog-search-and-view.js](loadtests/catalog-search-and-view.js)

Run the load test from the host:

```bash
k6 run loadtests/catalog-search-and-view.js
```

Or from Docker:

```bash
docker run --rm \
  --network assignment1_as_default \
  -e BASE_URL=http://nopcommerce \
  -v "$PWD/loadtests:/scripts" \
  grafana/k6 run /scripts/catalog-search-and-view.js
```

## Related Notes

- dashboard guide: [DASHBOARD.md](docs/DASHBOARD.md)
- privacy strategy: [PRIVACY.md](docs/PRIVACY.md)
- demo guide: [DEMO.md](docs/DEMO.md)
- critique: [CRITIQUE.md](docs/CRITIQUE.md)
- load test instructions: [LOAD_TESTING.md](docs/LOAD_TESTING.md)
