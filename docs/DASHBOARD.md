# Dashboard Guide

This document explains how to read the observability dashboard for the flow `Customer searches and views a product`.

## Access

Open these tools:

- Store: `http://localhost`
- Metrics endpoint: `http://localhost/metrics`
- Prometheus: `http://localhost:9090`
- Jaeger: `http://localhost:16686`
- Grafana: `http://localhost:33000`

Grafana credentials:

- username: `admin`
- password: `admin`

The dashboard name is `nopCommerce Observability`.

## Panels

### Empty Search Results Total

Metric:

- `nopcommerce_catalog_search_zero_results_total`

What it shows:

- the total number of searches that returned zero products

Why it matters:

- a high value can mean missing catalog coverage
- it can also mean search quality problems

### Empty Search Result Percentage (5m)

Metric:

- `100 * increase(nopcommerce_catalog_search_zero_results_total[5m]) / increase(nopcommerce_catalog_search_total[5m])`

What it shows:

- the percentage of searches in the last 5 minutes that returned zero products

Why it matters:

- it is easier to understand than a raw rate
- it tells you whether search quality is getting worse relative to total search volume

### Price Calculation Average Duration

Metric source:

- `nopcommerce_product_view_pricing_latency_seconds`

What it shows:

- the average time spent in pricing operations during product-page views

Why it matters:

- it shows whether pricing logic is becoming slower over time

### Price Calculation P95 Duration

Metric source:

- `nopcommerce_product_view_pricing_latency_seconds`

What it shows:

- the p95 latency of pricing operations

Why it matters:

- p95 is useful when a few slow requests are hidden by a good average

### 404 Not Found Percentage (5m)

Metric source:

- `http_server_request_duration_seconds_count`

What it shows:

- the percentage of store requests in the last 5 minutes that ended with `404`

Why it matters:

- it highlights broken links, wrong product URLs, and missing pages
- it can also reveal missing static files or theme assets

### Other 4xx Error Percentage (5m)

Metric source:

- `http_server_request_duration_seconds_count`

What it shows:

- the percentage of store requests in the last 5 minutes that ended with `4xx`, excluding `404`

Why it matters:

- it highlights client-side failures such as bad requests, validation failures, or permission problems
- it helps separate broken links from other user-facing request issues

### 5xx Server Error Percentage (5m)

Metric source:

- `http_server_request_duration_seconds_count`

What it shows:

- the percentage of store requests in the last 5 minutes that ended with `5xx`

Why it matters:

- it highlights server-side faults such as application exceptions, plugin failures, or database problems
- it is the clearest signal that the store is unhealthy

### Error panel threshold

All three error panels:

- show a percentage, not a rate line
- use the last 5 minutes of traffic
- exclude the `/metrics` endpoint from the denominator
- turn red above `0.1%`

### Traces

Traces are not embedded in the dashboard.

Open Jaeger directly:

- `http://localhost:16686`

Why it matters:

- the trace UI is clearer in Jaeger than in an embedded dashboard panel

## What To Expect

### Search with results

Action:

- search for a real product such as `laptop`

Expected result:

- traces appear in Jaeger
- search spans appear in the trace
- the zero-results metric does not increase

### Search with zero results

Action:

- search for a term such as `zzzz-no-product-123`

Expected result:

- `nopcommerce_catalog_search_zero_results_total` increases
- the percentage panel shows a value above `0%`
- the three error panels should still stay near `0%`

### Open a product page

Action:

- open a product page such as `/asus-laptop`

Expected result:

- pricing latency histogram gets new samples
- spans like `catalog.product.load` and `catalog.product.view` appear

## Useful Prometheus Queries

### Zero-result searches

```promql
sum(nopcommerce_catalog_search_zero_results_total)
```

### Zero-result search percentage in the last 5 minutes

```promql
100 * ((sum(increase(nopcommerce_catalog_search_zero_results_total[5m])) or vector(0)) / clamp_min((sum(increase(nopcommerce_catalog_search_total[5m])) or vector(0)), 1))
```

### Pricing latency count

```promql
nopcommerce_product_view_pricing_latency_seconds_count
```

### Pricing latency p95

```promql
histogram_quantile(0.95, sum by (le, pricing_operation) (rate(nopcommerce_product_view_pricing_latency_seconds_bucket[5m])))
```

### 404 percentage in the last 5 minutes

```promql
100 * ((sum(increase(http_server_request_duration_seconds_count{http_route!="/metrics",http_response_status_code="404"}[5m])) or vector(0)) / clamp_min((sum(increase(http_server_request_duration_seconds_count{http_route!="/metrics"}[5m])) or vector(0)), 1))
```

### Other 4xx percentage in the last 5 minutes

```promql
100 * (clamp_min(((sum(increase(http_server_request_duration_seconds_count{http_route!="/metrics",http_response_status_code=~"4.."}[5m])) or vector(0)) - (sum(increase(http_server_request_duration_seconds_count{http_route!="/metrics",http_response_status_code="404"}[5m])) or vector(0))), 0) / clamp_min((sum(increase(http_server_request_duration_seconds_count{http_route!="/metrics"}[5m])) or vector(0)), 1))
```

### 5xx percentage in the last 5 minutes

```promql
100 * ((sum(increase(http_server_request_duration_seconds_count{http_route!="/metrics",http_response_status_code=~"5.."}[5m])) or vector(0)) / clamp_min((sum(increase(http_server_request_duration_seconds_count{http_route!="/metrics"}[5m])) or vector(0)), 1))
```

## If A Panel Is Empty

Check these things:

- the store was installed with sample data or real products
- traffic was generated after the app started
- you waited at least a few scrape intervals
- `http://localhost/metrics` returns `200`
- Prometheus target `nopcommerce_web:80` is `up`

If the traces are empty, also check:

- Jaeger is running
- the application has `OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317`
- the flow was executed after the app started
