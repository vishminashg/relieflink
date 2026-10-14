using System.Text;

namespace ReliefLink.Api.Data;

/// <summary>
/// Hosting services such as Neon give a database address as a URI (<c>postgresql://user:password@host/db?sslmode=require</c>).
/// Npgsql wants <c>Host=...;Username=...</c>. This turns the first into the second so the URI can be pasted as it is.
/// A string that is already in Npgsql's own form is returned untouched, and null stays null.
/// </summary>
public static class ConnectionStringNormalizer
{
    public static string? Normalize(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;

        var text = connectionString.Trim();
        if (!text.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !text.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
            throw new InvalidOperationException("The database address looks like a URI but could not be read. Check that you copied all of it.");

        var parts = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(parts[0]);
        var password = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port;

        var sb = new StringBuilder();
        sb.Append("Host=").Append(uri.Host).Append(";Port=").Append(port);
        if (database.Length > 0) sb.Append(";Database=").Append(Quote(database));
        if (user.Length > 0) sb.Append(";Username=").Append(Quote(user));
        if (password.Length > 0) sb.Append(";Password=").Append(Quote(password));

        // Hosted databases need an encrypted connection. Anything the URI says about it is honoured; with no word, encrypt.
        var sslMode = "Require";
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase))
                sslMode = kv[1].ToLowerInvariant() switch
                {
                    "disable" => "Disable",
                    "allow" => "Allow",
                    "prefer" => "Prefer",
                    "verify-ca" => "VerifyCA",
                    "verify-full" => "VerifyFull",
                    _ => "Require"
                };
        }

        return sb.Append(";SSL Mode=").Append(sslMode).ToString();
    }

    /// <summary>A value with a semicolon, a quote or an edge space must be quoted in a connection string.</summary>
    private static string Quote(string value) =>
        value.IndexOfAny([';', '"', '\'', '=']) >= 0 || value != value.Trim()
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
