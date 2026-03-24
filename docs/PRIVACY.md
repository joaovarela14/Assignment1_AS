# Privacy Strategy

This document explains how sensitive data is handled in the observability setup.

## Goal

The goal is simple:

- export useful telemetry
- avoid exporting PII

For this assignment, that means search terms, emails, usernames, customer identifiers, and payment-related values should not leave the application as raw telemetry data.

## Where Redaction Happens

Redaction happens at the OpenTelemetry SDK layer.

Implementation:

- [SensitiveActivityProcessor.cs](../src/Presentation/Nop.Web.Framework/Infrastructure/Extensions/SensitiveActivityProcessor.cs)

This processor runs before spans are exported.

## Why This Layer Was Chosen

This was the best architectural choice for three reasons.

- It creates one enforcement point for many spans.
- It reduces the chance of developers forgetting to redact one tag manually.
- It keeps privacy logic out of business code.

This is cleaner than trying to remember redaction rules in every controller and service.

## What Is Redacted

The processor masks or removes tags such as:

- `db.statement`
- `db.query.text`
- `customer.email`
- `customer.id`
- `customer.username`
- `user.email`
- `user.id`
- `username`
- `search.term`
- payment-related fields

It also removes sensitive query strings from URLs, including keys such as:

- `q`
- `query`
- `search`
- `term`
- `email`
- `username`

## What We Still Keep

We still keep safe attributes that are useful for operators.

Examples:

- whether a search had a query
- query length instead of query value
- page size
- page number
- result counts
- product identifiers
- timing values

This keeps telemetry useful without exposing raw personal data.

## Logs

Telemetry log export is disabled by default.

That means:

- traces and metrics can be exported
- OpenTelemetry logs are only exported if explicitly enabled

This reduces the risk of leaking raw log bodies into the telemetry backend.

## Limits

This strategy is strong, but it is not magic.

- If a developer adds a new sensitive tag with a new name, the redaction rules may need to be updated.
- Application logs printed directly to the console are outside the span processor.
- Privacy still depends on good engineering discipline when new telemetry is added.

## Summary

The privacy strategy is:

- redact at the SDK layer
- export logs only when explicitly enabled
- keep safe counts, flags, and durations
- avoid raw personal data in spans and URLs
