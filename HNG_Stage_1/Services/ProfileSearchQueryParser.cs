using System.Globalization;
using System.Text.RegularExpressions;
using HNG_Stage_1.Models;

namespace HNG_Stage_1.Services
{
    public interface IProfileSearchQueryParser
    {
        bool TryParse(string query, out ProfileQueryParameters filters);
    }

    public partial class ProfileSearchQueryParser : IProfileSearchQueryParser
    {
        private static readonly Dictionary<string, string> CountryLookup = BuildCountryLookup();
        private static readonly HashSet<string> SupportedAgeGroups = new(StringComparer.OrdinalIgnoreCase)
        {
            "child",
            "teenager",
            "adult",
            "senior"
        };

        public bool TryParse(string query, out ProfileQueryParameters filters)
        {
            filters = new ProfileQueryParameters();

            if (string.IsNullOrWhiteSpace(query))
            {
                return false;
            }

            var normalized = Normalize(query);
            var interpreted = false;

            var mentionsMale = ContainsAny(normalized, "male", "males", "man", "men", "boy", "boys");
            var mentionsFemale = ContainsAny(normalized, "female", "females", "woman", "women", "girl", "girls");

            if (mentionsMale && !mentionsFemale)
            {
                filters.Gender = "male";
                interpreted = true;
            }
            else if (mentionsFemale && !mentionsMale)
            {
                filters.Gender = "female";
                interpreted = true;
            }
            else if (mentionsMale && mentionsFemale)
            {
                interpreted = true;
            }

            if (ContainsWord(normalized, "young"))
            {
                filters.MinAge = Max(filters.MinAge, 16);
                filters.MaxAge = Min(filters.MaxAge, 24);
                interpreted = true;
            }

            foreach (var ageGroup in SupportedAgeGroups)
            {
                if (ContainsWord(normalized, ageGroup) || ContainsWord(normalized, $"{ageGroup}s"))
                {
                    filters.AgeGroup = ageGroup;
                    interpreted = true;
                    break;
                }
            }

            var aboveMatch = AboveAgeRegex().Match(normalized);
            if (aboveMatch.Success)
            {
                filters.MinAge = Max(filters.MinAge, int.Parse(aboveMatch.Groups["age"].Value, CultureInfo.InvariantCulture));
                interpreted = true;
            }

            var belowMatch = BelowAgeRegex().Match(normalized);
            if (belowMatch.Success)
            {
                filters.MaxAge = Min(filters.MaxAge, int.Parse(belowMatch.Groups["age"].Value, CultureInfo.InvariantCulture));
                interpreted = true;
            }

            var betweenMatch = BetweenAgeRegex().Match(normalized);
            if (betweenMatch.Success)
            {
                filters.MinAge = Max(filters.MinAge, int.Parse(betweenMatch.Groups["min"].Value, CultureInfo.InvariantCulture));
                filters.MaxAge = Min(filters.MaxAge, int.Parse(betweenMatch.Groups["max"].Value, CultureInfo.InvariantCulture));
                interpreted = true;
            }

            var fromMatch = FromCountryRegex().Match(normalized);
            if (fromMatch.Success)
            {
                var countryToken = fromMatch.Groups["country"].Value.Trim();
                if (TryResolveCountry(countryToken, out var countryCode))
                {
                    filters.CountryId = countryCode;
                    interpreted = true;
                }
            }
            else
            {
                foreach (var entry in CountryLookup.OrderByDescending(c => c.Key.Length))
                {
                    if (ContainsWord(normalized, entry.Key))
                    {
                        filters.CountryId = entry.Value;
                        interpreted = true;
                        break;
                    }
                }
            }

            if (filters.MinAge.HasValue && filters.MaxAge.HasValue && filters.MinAge > filters.MaxAge)
            {
                return false;
            }

            return interpreted;
        }

        private static string Normalize(string value)
        {
            var normalized = value.Trim().ToLowerInvariant();
            normalized = normalized.Replace("-", " ");
            normalized = MultiSpaceRegex().Replace(normalized, " ");
            return normalized;
        }

        private static bool TryResolveCountry(string token, out string countryCode)
        {
            countryCode = string.Empty;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var normalized = Normalize(token);
            if (!CountryLookup.TryGetValue(normalized, out var resolvedCountryCode))
            {
                return false;
            }

            countryCode = resolvedCountryCode;
            return true;
        }

        private static Dictionary<string, string> BuildCountryLookup()
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                try
                {
                    var region = new RegionInfo(culture.Name);
                    if (region.TwoLetterISORegionName.Length != 2)
                    {
                        continue;
                    }

                    lookup[Normalize(region.EnglishName)] = region.TwoLetterISORegionName;
                    lookup[Normalize(region.NativeName)] = region.TwoLetterISORegionName;
                    if (!string.IsNullOrWhiteSpace(region.DisplayName))
                    {
                        lookup[Normalize(region.DisplayName)] = region.TwoLetterISORegionName;
                    }
                    lookup[Normalize(region.TwoLetterISORegionName)] = region.TwoLetterISORegionName;
                }
                catch (ArgumentException)
                {
                }
            }

            lookup["nigeria"] = "NG";
            lookup["kenya"] = "KE";
            lookup["angola"] = "AO";
            lookup["benin"] = "BJ";

            return lookup;
        }

        private static bool ContainsAny(string source, params string[] values) => values.Any(value => ContainsWord(source, value));

        private static bool ContainsWord(string source, string value) => Regex.IsMatch(source, $@"\b{Regex.Escape(value)}\b", RegexOptions.IgnoreCase);

        private static int? Max(int? left, int right) => !left.HasValue || right > left.Value ? right : left;

        private static int? Min(int? left, int right) => !left.HasValue || right < left.Value ? right : left;

        [GeneratedRegex(@"\babove\s+(?<age>\d{1,3})\b|\bover\s+(?<age>\d{1,3})\b|\bolder\s+than\s+(?<age>\d{1,3})\b", RegexOptions.IgnoreCase)]
        private static partial Regex AboveAgeRegex();

        [GeneratedRegex(@"\bbelow\s+(?<age>\d{1,3})\b|\bunder\s+(?<age>\d{1,3})\b|\byounger\s+than\s+(?<age>\d{1,3})\b", RegexOptions.IgnoreCase)]
        private static partial Regex BelowAgeRegex();

        [GeneratedRegex(@"\bbetween\s+(?<min>\d{1,3})\s+and\s+(?<max>\d{1,3})\b", RegexOptions.IgnoreCase)]
        private static partial Regex BetweenAgeRegex();

        [GeneratedRegex(@"\bfrom\s+(?<country>[a-z\s]{2,})$", RegexOptions.IgnoreCase)]
        private static partial Regex FromCountryRegex();

        [GeneratedRegex(@"\s+", RegexOptions.IgnoreCase)]
        private static partial Regex MultiSpaceRegex();
    }
}
