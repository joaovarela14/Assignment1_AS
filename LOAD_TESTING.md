# Load Testing

## Flow

Selected flow: `Customer searches and views a product`

The load test drives:

- homepage request
- search request with results
- product page request
- periodic zero-results search to exercise the custom counter

Prerequisite:

- complete the nopCommerce installation first and keep the SQL Server volume so the catalog stays available across rebuilds

## Script

Script path:

- [catalog-search-and-view.js](/home/varela/Desktop/AS/Derivables/Individual/Assignment1_AS/loadtests/catalog-search-and-view.js)

## Run with Docker

The script is designed to run from a `k6` container on the same Docker network as nopCommerce:

```bash
docker run --rm \
  --network assignment1_as_default \
  -e BASE_URL=http://nopcommerce \
  -v "$PWD/loadtests:/scripts" \
  grafana/k6 run /scripts/catalog-search-and-view.js
```

## Run from host machine

If `k6` is installed locally and nopCommerce is exposed on port `80`, run:

```bash
k6 run loadtests/catalog-search-and-view.js
```

The script defaults to `BASE_URL=http://localhost` for host-based runs.

## Optional environment overrides

```bash
docker run --rm \
  --network assignment1_as_default \
  -e BASE_URL=http://nopcommerce \
  -e SEARCH_TERM=laptop \
  -e ZERO_RESULT_TERM=zzzz-no-product-123 \
  -e PRODUCT_PATHS=/asus-laptop,/lenovo-thinkpad-carbon-laptop \
  -v "$PWD/loadtests:/scripts" \
  grafana/k6 run /scripts/catalog-search-and-view.js
```

## What to watch during the test

- Grafana: `http://localhost:33000`
- Jaeger: `http://localhost:16686`
- Prometheus: `http://localhost:9090`

Expected signals:

- `nopcommerce_catalog_search_zero_results_total` increases because the script periodically sends a zero-results search.
- `nopcommerce_product_view_pricing_latency_seconds` gains samples because the script opens product detail pages.
- The error-rate panel stays near zero under healthy load.
- Jaeger shows repeated traces for `catalog.search`, `catalog.product.search`, `catalog.product.load`, and `catalog.product.view`.
