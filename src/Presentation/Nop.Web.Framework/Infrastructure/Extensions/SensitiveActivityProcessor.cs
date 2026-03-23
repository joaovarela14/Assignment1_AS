using System.Diagnostics;
using OpenTelemetry;

namespace Nop.Web.Framework.Infrastructure.Extensions;

/// <summary>
/// Removes or masks sensitive activity tags before export.
/// </summary>
public sealed class SensitiveActivityProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> SensitiveTagNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "customer.email",
        "customer.id",
        "customer.username",
        "db.query.text",
        "db.statement",
        "email",
        "enduser.id",
        "payment.card.number",
        "payment.details",
        "payment.method",
        "search.term",
        "user.id",
        "user.email",
        "user.name",
        "username"
    };

    private static readonly string[] SensitiveQueryKeys =
    [
        "customeremail",
        "email",
        "q",
        "query",
        "search",
        "term",
        "user",
        "username"
    ];

    public override void OnEnd(Activity data)
    {
        ArgumentNullException.ThrowIfNull(data);

        foreach (var tag in data.TagObjects.ToList())
        {
            if (SensitiveTagNames.Contains(tag.Key))
            {
                data.SetTag(tag.Key, "[REDACTED]");
                continue;
            }

            if (tag.Value is not string stringValue)
                continue;

            if (tag.Key.Equals("http.url", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Equals("url.full", StringComparison.OrdinalIgnoreCase))
            {
                data.SetTag(tag.Key, RedactUrl(stringValue));
                continue;
            }

            if (tag.Key.Equals("url.query", StringComparison.OrdinalIgnoreCase) && ContainsSensitiveQuery(stringValue))
            {
                data.SetTag(tag.Key, "[REDACTED]");
                continue;
            }

            if (tag.Key.Contains("search", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("payment", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("statement", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("query", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("customer", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("enduser", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("user.id", StringComparison.OrdinalIgnoreCase) ||
                tag.Key.Contains("username", StringComparison.OrdinalIgnoreCase))
            {
                data.SetTag(tag.Key, "[REDACTED]");
            }
        }
    }

    private static string RedactUrl(string value)
    {
        var separatorIndex = value.IndexOf('?', StringComparison.Ordinal);
        if (separatorIndex < 0)
            return value;

        var query = value[(separatorIndex + 1)..];
        if (!ContainsSensitiveQuery(query))
            return value;

        return value[..separatorIndex];
    }

    private static bool ContainsSensitiveQuery(string query)
    {
        var normalizedQuery = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return false;

        return normalizedQuery
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)[0])
            .Any(key => SensitiveQueryKeys.Contains(key, StringComparer.OrdinalIgnoreCase));
    }
}
