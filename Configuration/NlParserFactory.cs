using DataPersistentApi.Services;

namespace DataPersistentApi.Configuration;

public static class NlParserFactory
{
    public static NlParser Create(string contentRootPath)
    {
        var countryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var seedPath = Path.Combine(contentRootPath, "Data/seed/profiles-2026.json");
            if (!File.Exists(seedPath))
            {
                return new NlParser(countryMap);
            }

            var txt = File.ReadAllText(seedPath);
            using var doc = System.Text.Json.JsonDocument.Parse(txt);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !root.TryGetProperty("profiles", out var arr) ||
                arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return new NlParser(countryMap);
            }

            foreach (var el in arr.EnumerateArray())
            {
                if (!el.TryGetProperty("country_name", out var countryName) ||
                    !el.TryGetProperty("country_id", out var countryId))
                {
                    continue;
                }

                var name = countryName.GetString();
                var id = countryId.GetString();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var key = name.ToLowerInvariant();
                if (!countryMap.ContainsKey(key))
                {
                    countryMap[key] = id.ToUpperInvariant();
                }
            }
        }
        catch
        {
            // ignore any errors reading seed file
        }

        return new NlParser(countryMap);
    }
}
