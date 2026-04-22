using System.Text.Json;
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
            var seedFilePath = Path.Combine(_environment.ContentRootPath, "SeedData", "profiles.json");
            if (!File.Exists(seedFilePath))
            {
                _logger.LogInformation("Seed file not found at {SeedFilePath}. Skipping profile seed.", seedFilePath);
                return;
            }

            await using var stream = File.OpenRead(seedFilePath);
            var seedProfiles = await JsonSerializer.DeserializeAsync<List<SeedProfile>>(stream, cancellationToken: cancellationToken);
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
