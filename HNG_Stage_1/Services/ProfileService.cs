using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HNG_Stage_1.Data;
using HNG_Stage_1.Models;
using UUIDNext;

namespace HNG_Stage_1.Services
{
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }

    public class ProfileService : IProfileService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ExternalApiService _externalApiService;

        // Using ExternalApiService directly since it has the specific return type matching our setup,
        // or we could use IExternalApiService if we registered the specific interface correctly.
        public ProfileService(ApplicationDbContext dbContext, ExternalApiService externalApiService)
        {
            _dbContext = dbContext;
            _externalApiService = externalApiService;
        }

        public async Task<(Profile Profile, bool IsCreated)> CreateOrGetProfileAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ValidationException("name must be provided");
            }

            var lowerName = name.ToLowerInvariant();

            // Check if exists
            var existingProfile = await _dbContext.Profiles
                .FirstOrDefaultAsync(p => p.Name.ToLower() == lowerName);

            if (existingProfile != null)
            {
                return (existingProfile, false);
            }

            // Fetch from APIs
            var (genderize, agify, nationalize) = await _externalApiService.FetchAllDataAsync(lowerName);

            // Classification
            string ageGroup = ClassifyAgeGroup(agify.Age.Value);
            var bestCountry = nationalize.Country.OrderByDescending(c => c.Probability).FirstOrDefault();

            if (bestCountry == null)
            {
                throw new ExternalApiException("Nationalize returned an invalid response");
            }

            // Create new profile
            var profile = new Profile
            {
                Id = Uuid.NewSequential().ToString(), // UUID v7 equivalent in UUIDNext
                Name = lowerName,
                Gender = genderize.Gender!,
                GenderProbability = genderize.Probability,
                SampleSize = genderize.Count,
                Age = agify.Age.Value,
                AgeGroup = ageGroup,
                CountryId = bestCountry.Country_id,
                CountryProbability = bestCountry.Probability,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Profiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            return (profile, true);
        }

        public async Task<Profile?> GetProfileByIdAsync(string id)
        {
            return await _dbContext.Profiles.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<(int Count, List<Profile> Data)> GetAllProfilesAsync(string? gender, string? countryId, string? ageGroup)
        {
            var query = _dbContext.Profiles.AsQueryable();

            if (!string.IsNullOrWhiteSpace(gender))
            {
                var lowerGender = gender.ToLowerInvariant();
                query = query.Where(p => p.Gender.ToLower() == lowerGender);
            }

            if (!string.IsNullOrWhiteSpace(countryId))
            {
                var lowerCountryId = countryId.ToLowerInvariant();
                query = query.Where(p => p.CountryId.ToLower() == lowerCountryId);
            }

            if (!string.IsNullOrWhiteSpace(ageGroup))
            {
                var lowerAgeGroup = ageGroup.ToLowerInvariant();
                query = query.Where(p => p.AgeGroup.ToLower() == lowerAgeGroup);
            }

            var results = await query.ToListAsync();
            return (results.Count, results);
        }

        public async Task<bool> DeleteProfileAsync(string id)
        {
            var profile = await _dbContext.Profiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile == null) return false;

            _dbContext.Profiles.Remove(profile);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        private string ClassifyAgeGroup(int age)
        {
            if (age >= 0 && age <= 12) return "child";
            if (age >= 13 && age <= 19) return "teenager";
            if (age >= 20 && age <= 59) return "adult";
            return "senior"; // 60+
        }
    }
}
