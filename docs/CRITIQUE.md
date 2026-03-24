# Critique

## What helped and what hindered

nopCommerce helped this work in three important ways:

First, the system is layered in a way that makes the main business path readable. For the flow `Customer searches and views a product`, it is possible to start at the web layer, move into `Nop.Services`, and then reach the data access path without guessing too much. That made it practical to place spans at the HTTP entry point, in the catalog service layer, and around pricing work.

Second, nopCommerce already has an internal event mechanism through `IEventPublisher`. That was very useful for the product-view part of the flow. Instead of editing more service code, I could attach observability through an event consumer and keep the change close to the boundary of the system. This is the kind of extension point that makes a large inherited codebase easier to instrument safely.

Third, dependency injection is already part of the platform, so cross-cutting concerns can be introduced with a surgical approach. The pricing latency metric is a good example. It was possible to wrap `IPriceCalculationService` with a decorator and collect a useful metric without rewriting the pricing logic itself.

At the same time, the codebase also created friction.

The biggest issue is that observability is not a first-class architectural concern in nopCommerce. There is no shared telemetry abstraction, no common span naming policy, no central place where domain metrics live, and no built-in privacy policy for telemetry. Because of that, instrumentation has to be added case by case.

Another difficulty is that event coverage is stronger on write paths than on read paths. Search and product view are read-heavy flows. They do not naturally expose the same event coverage as order placement or entity updates, so some manual tracing was still necessary in the controller and service layers.

A third problem is hidden coupling. In some areas, the code still relies on patterns like service location or indirect resolution, which makes the actual runtime path harder to see. That does not stop instrumentation, but it makes it harder to know whether a decorator or event hook will catch every relevant execution path.

There is also an important technical mismatch between the assignment wording and the real codebase: this nopCommerce version does not use Entity Framework Core in the catalog path. It uses `linq2db` on top of ADO.NET providers. Architecturally, that matters because the right automatic tracing choice is provider instrumentation such as `SqlClient`, not `AddEntityFrameworkCoreInstrumentation()`. In other words, inherited systems force you to instrument what is really there, not what the ideal stack would have been.

## What I would change going forward

If I were making architectural decisions for nopCommerce going forward, I would not start with a large rewrite. I would make three focused changes.

The first change would be to create a small observability layer in shared infrastructure. That layer would own the `ActivitySource`, `Meter`, naming conventions, standard tags, and privacy rules. The cost is low, and the benefit is high: instrumentation becomes more consistent and less scattered.

The second change would be to standardise redaction at the SDK boundary and treat it as policy, not as developer discipline. In this assignment, the redaction processor was the cleanest place to mask or remove sensitive fields before export. I would keep that direction and make it explicit in the architecture. The cost is low to medium, but the benefit is strong because privacy mistakes are expensive.

The third change would be to improve observability boundaries on important read flows. nopCommerce already has decent event coverage for writes, but flows like search, pricing, and product view would be easier to observe if the system exposed clearer domain events or decorators by design. The cost is medium because it touches multiple services and conventions, but it is still much cheaper than a major refactor.

The one change I would not recommend as part of an observability task alone is a full cleanup of the architecture to remove every service-location style dependency and fully isolate layers. That would improve long-term maintainability, but the cost is high and the risk is real in a mature e-commerce platform. For this project, targeted observability improvements are worth making now; a broad architectural cleanup should only happen as part of a longer programme of platform work.

## The surgical change and why it was necessary

The most surgical change in this work was the pricing instrumentation. Pricing calculation is operationally important, but it sits inside core business logic and is called from many places. Editing the pricing service directly would have mixed telemetry concerns into business code and increased the risk of behaviour change.

Instead, I wrapped `IPriceCalculationService` with `ObservedPriceCalculationService` at registration time. That kept the business logic intact and limited the observability code to a decorator plus a registration change in startup. It was necessary because the assignment required a metric that would tell an operator when pricing logic starts slowing down during product views, and there was no existing event or built-in metric that captured that.

The other surgical choice was to use `IEventPublisher` for product-view observability rather than adding more code inside the service layer. That was worth doing because it matched the architecture nopCommerce already uses for extension. It reduced code churn and kept the instrumentation close to infrastructure boundaries, which is exactly the right tradeoff in an inherited system.

So the general lesson is this: nopCommerce is observable enough to support targeted instrumentation, but not observable enough that the work disappears into configuration. The right approach is not to refactor the platform into an ideal architecture. The right approach is to use its existing seams carefully, add a small amount of shared telemetry infrastructure, and enforce privacy at the export boundary.
