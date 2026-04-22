using System.Text.Json;
using System.Text.Json.Serialization;
using HNG_Stage_1.Data;
using HNG_Stage_1.Models;
using Microsoft.EntityFrameworkCore;
using UUIDNext;

namespace HNG_Stage_1.Services
{
    public class DatabaseSeeder
    {
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
            var seedFilePath = ResolveSeedFilePath();
            if (seedFilePath == null)
            {
                _logger.LogInformation("Seed file not found. Skipping profile seed.");
                return;
            }

            var seedProfiles = await LoadSeedProfilesAsync(seedFilePath, cancellationToken);
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

        private string? ResolveSeedFilePath()
        {
            var candidatePaths = new[]
            {
                Path.Combine(_environment.ContentRootPath, "SeedData", "profiles.json"),
                Path.Combine(AppContext.BaseDirectory, "SeedData", "profiles.json"),
                Path.Combine(_environment.ContentRootPath, "HNG_Stage_1", "SeedData", "profiles.json")
            };

            var seedFilePath = candidatePaths.FirstOrDefault(File.Exists);
            if (seedFilePath != null)
            {
                _logger.LogInformation("Using seed file at {SeedFilePath}.", seedFilePath);
            }

            return seedFilePath;
        }

        private static async Task<List<SeedProfile>?> LoadSeedProfilesAsync(string seedFilePath, CancellationToken cancellationToken)
        {
            var json = await File.ReadAllTextAsync(seedFilePath, cancellationToken);
            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var document = JsonSerializer.Deserialize<SeedProfilesDocument>(json, serializerOptions);
            if (document?.Profiles != null && document.Profiles.Count > 0)
            {
                return document.Profiles;
            }

            return JsonSerializer.Deserialize<List<SeedProfile>>(json, serializerOptions);
        }

        private class SeedProfilesDocument
        {
            [JsonPropertyName("profiles")]
            public List<SeedProfile> Profiles { get; set; } = new();
        }

        private class SeedProfile
        {
            [JsonPropertyName("name")]
            public string Name { get; set; } = string.Empty;

            [JsonPropertyName("gender")]
            public string Gender { get; set; } = string.Empty;

            [JsonPropertyName("gender_probability")]
            public double GenderProbability { get; set; }

            [JsonPropertyName("age")]
            public int Age { get; set; }

            [JsonPropertyName("age_group")]
            public string AgeGroup { get; set; } = string.Empty;

            [JsonPropertyName("country_id")]
            public string CountryId { get; set; } = string.Empty;

            [JsonPropertyName("country_name")]
            public string CountryName { get; set; } = string.Empty;

            [JsonPropertyName("country_probability")]
            public double CountryProbability { get; set; }

            [JsonPropertyName("created_at")]
            public DateTime? CreatedAt { get; set; }
        }
    }
}
