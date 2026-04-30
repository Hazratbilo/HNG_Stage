using System.Text.Json;
using HNG_Stage_1.Data;
using HNG_Stage_1.Models;
using Microsoft.EntityFrameworkCore;
using UUIDNext;

namespace HNG_Stage_1.Services
{
    public class DatabaseSeeder
    {
        private const string SeededAdminGithubId = "seed-admin-github";
        private const string SeededAnalystGithubId = "seed-analyst-github";

        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<DatabaseSeeder> _logger;
        private readonly IWebHostEnvironment _environment;

        public DatabaseSeeder(ApplicationDbContext dbContext, ILogger<DatabaseSeeder> logger, IWebHostEnvironment environment)
        {
            _dbContext = dbContext;
            _logger = logger;
            _environment = environment;
        }

        public async Task SeedProfilesAsync(CancellationToken cancellationToken = default)
        {
            var seedFilePath = Path.Combine(_environment.ContentRootPath, "SeedData", "profiles.json");
            if (!File.Exists(seedFilePath))
            {
                _logger.LogInformation("Seed file not found at {SeedFilePath}. Skipping profile seed.", seedFilePath);
                return;
            }

            await using var stream = File.OpenRead(seedFilePath);
            using var seedDocument = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var seedProfiles = ParseSeedProfiles(seedDocument.RootElement);
            if (seedProfiles == null || seedProfiles.Count == 0)
            {
                _logger.LogInformation("Seed file was empty. Skipping profile seed.");
                return;
            }

            var existingNames = (await _dbContext.Profiles
                .Select(profile => profile.Name)
                .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var profilesToInsert = new List<Profile>();
            foreach (var seedProfile in seedProfiles)
            {
                var normalizedName = seedProfile.Name.Trim().ToLowerInvariant();
                if (existingNames.Contains(normalizedName))
                {
                    continue;
                }

                profilesToInsert.Add(new Profile
                {
                    Id = Uuid.NewDatabaseFriendly(Database.SQLite).ToString(),
                    Name = normalizedName,
                    Gender = seedProfile.Gender.Trim().ToLowerInvariant(),
                    GenderProbability = seedProfile.GenderProbability,
                    Age = seedProfile.Age,
                    AgeGroup = seedProfile.AgeGroup.Trim().ToLowerInvariant(),
                    CountryId = seedProfile.CountryId.Trim().ToUpperInvariant(),
                    CountryName = seedProfile.CountryName.Trim(),
                    CountryProbability = seedProfile.CountryProbability,
                    CreatedAt = seedProfile.CreatedAt?.ToUniversalTime() ?? DateTime.UtcNow
                });

                existingNames.Add(normalizedName);
            }

            if (profilesToInsert.Count == 0)
            {
                _logger.LogInformation("No new profiles found during seed.");
                return;
            }

            await _dbContext.Profiles.AddRangeAsync(profilesToInsert, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} profiles.", profilesToInsert.Count);
        }

        private static List<SeedProfile> ParseSeedProfiles(JsonElement rootElement)
        {
            JsonElement profilesElement;

            if (rootElement.ValueKind == JsonValueKind.Array)
            {
                profilesElement = rootElement;
            }
            else if (rootElement.ValueKind == JsonValueKind.Object &&
                     rootElement.TryGetProperty("profiles", out var nestedProfiles) &&
                     nestedProfiles.ValueKind == JsonValueKind.Array)
            {
                profilesElement = nestedProfiles;
            }
            else
            {
                throw new JsonException("Seed profiles JSON must be an array or an object containing a 'profiles' array.");
            }

            var profiles = new List<SeedProfile>();
            foreach (var profileElement in profilesElement.EnumerateArray())
            {
                profiles.Add(new SeedProfile
                {
                    Name = GetRequiredString(profileElement, "name"),
                    Gender = GetRequiredString(profileElement, "gender"),
                    GenderProbability = GetRequiredDouble(profileElement, "genderProbability", "gender_probability"),
                    Age = GetRequiredInt(profileElement, "age"),
                    AgeGroup = GetRequiredString(profileElement, "ageGroup", "age_group"),
                    CountryId = GetRequiredString(profileElement, "countryId", "country_id"),
                    CountryName = GetRequiredString(profileElement, "countryName", "country_name"),
                    CountryProbability = GetRequiredDouble(profileElement, "countryProbability", "country_probability"),
                    CreatedAt = GetOptionalDateTime(profileElement, "createdAt", "created_at")
                });
            }

            return profiles;
        }

        private static string GetRequiredString(JsonElement element, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString() ?? string.Empty;
                }
            }

            throw new JsonException($"Missing required string property. Expected one of: {string.Join(", ", propertyNames)}");
        }

        private static double GetRequiredDouble(JsonElement element, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (element.TryGetProperty(propertyName, out var value) && value.TryGetDouble(out var result))
                {
                    return result;
                }
            }

            throw new JsonException($"Missing required numeric property. Expected one of: {string.Join(", ", propertyNames)}");
        }

        private static int GetRequiredInt(JsonElement element, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result))
                {
                    return result;
                }
            }

            throw new JsonException($"Missing required integer property. Expected one of: {string.Join(", ", propertyNames)}");
        }

        private static DateTime? GetOptionalDateTime(JsonElement element, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var parsedDateTime))
                {
                    return parsedDateTime;
                }
            }

            return null;
        }

        public async Task SeedUsersAsync(CancellationToken cancellationToken = default)
        {
            await EnsureUserAsync(
                githubId: SeededAdminGithubId,
                username: "seed-admin",
                email: "seed-admin@insighta.local",
                role: "admin",
                cancellationToken);

            await EnsureUserAsync(
                githubId: SeededAnalystGithubId,
                username: "seed-analyst",
                email: "seed-analyst@insighta.local",
                role: "analyst",
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task EnsureUserAsync(
            string githubId,
            string username,
            string email,
            string role,
            CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(existingUser => existingUser.GithubId == githubId, cancellationToken);

            if (user == null)
            {
                user = new User
                {
                    Id = Uuid.NewDatabaseFriendly(Database.SQLite).ToString(),
                    GithubId = githubId,
                    Username = username,
                    Email = email,
                    Role = role,
                    IsActive = true,
                    AvatarUrl = null,
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.Users.AddAsync(user, cancellationToken);
                _logger.LogInformation("Seeded {Role} test user {Username}.", role, username);
                return;
            }

            user.Username = username;
            user.Email = email;
            user.Role = role;
            user.IsActive = true;
        }

        private class SeedProfile
        {
            public string Name { get; set; } = string.Empty;
            public string Gender { get; set; } = string.Empty;
            public double GenderProbability { get; set; }
            public int Age { get; set; }
            public string AgeGroup { get; set; } = string.Empty;
            public string CountryId { get; set; } = string.Empty;
            public string CountryName { get; set; } = string.Empty;
            public double CountryProbability { get; set; }
            public DateTime? CreatedAt { get; set; }
        }
    }
}
