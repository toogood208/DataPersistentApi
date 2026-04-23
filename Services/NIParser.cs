using System.Text.RegularExpressions;

namespace DataPersistentApi.Services;

public class NlParser
{
    private readonly Dictionary<string,string> _countryMap;

    public NlParser(Dictionary<string,string>? countryMap = null)
    {
        _countryMap = countryMap ?? new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            ["nigeria"] = "NG",
            ["kenya"]    = "KE",
            ["angola"]   = "AO",
            ["cameroon"] = "CM",
            // expand list (Data/countries.json recommended)
        };
    }

    // Return false if unable to interpret
    public bool TryParse(string q, out QueryOptions opts)
    {
        opts = new QueryOptions();
        if (string.IsNullOrWhiteSpace(q)) return false;
        var s = q.ToLowerInvariant();

        var hasMale = Regex.IsMatch(s, @"\b(male|males|men|boy|boys)\b");
        var hasFemale = Regex.IsMatch(s, @"\b(female|females|women|girls)\b");
        if (hasMale && !hasFemale) opts.Gender = "male";
        if (hasFemale && !hasMale) opts.Gender = "female";

        if (s.Contains("young")) { opts.MinAge = Math.Max(opts.MinAge ?? 0, 16); opts.MaxAge = Math.Min(opts.MaxAge ?? int.MaxValue, 24); }
        if (s.Contains("teen") || s.Contains("teenager")) opts.AgeGroup = "teenager";
        if (s.Contains("child") || s.Contains("children")) opts.AgeGroup = "child";
        if (s.Contains("adult")) opts.AgeGroup = "adult";
        if (s.Contains("senior") || s.Contains("elder")) opts.AgeGroup = "senior";

        var m = Regex.Match(s, @"(?:above|over)\s+(\d{1,3})");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) opts.MinAge = Math.Max(opts.MinAge ?? 0, n);

        m = Regex.Match(s, @"(?:below|under)\s+(\d{1,3})");
        if (m.Success && int.TryParse(m.Groups[1].Value, out n)) opts.MaxAge = Math.Min(opts.MaxAge ?? int.MaxValue, n);

        m = Regex.Match(s, @"between\s+(\d{1,3})\s+and\s+(\d{1,3})");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var a) && int.TryParse(m.Groups[2].Value, out var b))
        { opts.MinAge = Math.Max(opts.MinAge ?? 0, Math.Min(a,b)); opts.MaxAge = Math.Min(opts.MaxAge ?? int.MaxValue, Math.Max(a,b)); }

        foreach (var kv in _countryMap)
            if (s.Contains(kv.Key)) { opts.CountryId = kv.Value.ToUpperInvariant(); break; }

        // require at least one filter interpretation
        if (opts.Gender==null && opts.AgeGroup==null && opts.MinAge==null && opts.MaxAge==null && opts.CountryId==null)
            return false;

        return true;
    }
}


