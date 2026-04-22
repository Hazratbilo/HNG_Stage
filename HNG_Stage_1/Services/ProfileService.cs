using HNG_Stage_1.Data;
using HNG_Stage_1.Models;
using Microsoft.EntityFrameworkCore;
using UUIDNext;

namespace HNG_Stage_1.Services
{
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }
    }

    public class InvalidQueryParametersException : Exception
    {
        public InvalidQueryParametersException(string message) : base(message) { }
    }

    public class UnableToInterpretQueryException : Exception
    {
        public UnableToInterpretQueryException(string message) : base(message) { }
    }

    public class ProfileService : IProfileService
    {
        private const int MaxLimit = 50;

        private readonly ApplicationDbContext _dbContext;
        private readonly IExternalApiService _externalApiService;
        private readonly IProfileSearchQueryParser _profileSearchQueryParser;

        public ProfileService(
            ApplicationDbContext dbContext,
            IExternalApiService externalApiService,
            IProfileSearchQueryParser profileSearchQueryParser)
        {
            _dbContext = dbContext;
            _externalApiService = externalApiService;
            _profileSearchQueryParser = profileSearchQueryParser;
        }

        public async Task<(Profile Profile, bool IsCreated)> CreateOrGetProfileAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ValidationException("Missing or empty name");
            }

            var normalizedName = name.Trim().ToLowerInvariant();

            var existingProfile = await _dbContext.Profiles
                .FirstOrDefaultAsync(profile => profile.Name == normalizedName);

            if (existingProfile != null)
            {
                return (existingProfile, false);
            }

            var (genderize, agify, nationalize) = await _externalApiService.FetchAllDataAsync(normalizedName);
            if (!agify.Age.HasValue)
            {
                throw new ExternalApiException("Agify returned an invalid response");
            }

            var bestCountry = nationalize.Country
                .OrderByDescending(country => country.Probability)
                .FirstOrDefault();

            if (bestCountry == null)
            {
                throw new ExternalApiException("Nationalize returned an invalid response");
            }

            var countryName = ResolveCountryName(bestCountry.Country_id);

            var profile = new Profile
            {
                Id = Uuid.NewSequential().ToString(),
                Name = normalizedName,
                Gender = genderize.Gender!.ToLowerInvariant(),
                GenderProbability = genderize.Probability,
                Age = agify.Age.Value,
                AgeGroup = ClassifyAgeGroup(agify.Age.Value),
                CountryId = bestCountry.Country_id.ToUpperInvariant(),
                CountryName = countryName,
                CountryProbability = bestCountry.Probability,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Profiles.Add(profile);
            await _dbContext.SaveChangesAsync();

            return (profile, true);
        }

        public Task<Profile?> GetProfileByIdAsync(string id) =>
            _dbContext.Profiles.FirstOrDefaultAsync(profile => profile.Id == id);

        public Task<PagedProfilesResult> GetProfilesAsync(ProfileQueryParameters parameters) =>
            QueryProfilesAsync(parameters);

        public async Task<PagedProfilesResult> SearchProfilesAsync(string query, int page, int limit)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ValidationException("Missing or empty parameter");
            }

            if (!_profileSearchQueryParser.TryParse(query, out var filters))
            {
                throw new UnableToInterpretQueryException("Unable to interpret query");
            }

            filters.Page = page;
            filters.Limit = limit;

            return await QueryProfilesAsync(filters);
        }

        public async Task<bool> DeleteProfileAsync(string id)
        {
            var profile = await _dbContext.Profiles.FirstOrDefaultAsync(candidate => candidate.Id == id);
            if (profile == null)
            {
                return false;
            }

            _dbContext.Profiles.Remove(profile);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        private async Task<PagedProfilesResult> QueryProfilesAsync(ProfileQueryParameters parameters)
        {
            ValidateQueryParameters(parameters);

            IQueryable<Profile> query = _dbContext.Profiles.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(parameters.Gender))
            {
                var gender = parameters.Gender.Trim().ToLowerInvariant();
                query = query.Where(profile => profile.Gender == gender);
            }

            if (!string.IsNullOrWhiteSpace(parameters.AgeGroup))
            {
                var ageGroup = parameters.AgeGroup.Trim().ToLowerInvariant();
                query = query.Where(profile => profile.AgeGroup == ageGroup);
            }

            if (!string.IsNullOrWhiteSpace(parameters.CountryId))
            {
                var countryId = parameters.CountryId.Trim().ToUpperInvariant();
                query = query.Where(profile => profile.CountryId == countryId);
            }

            if (parameters.MinAge.HasValue)
            {
                query = query.Where(profile => profile.Age >= parameters.MinAge.Value);
            }

            if (parameters.MaxAge.HasValue)
            {
                query = query.Where(profile => profile.Age <= parameters.MaxAge.Value);
            }

            if (parameters.MinGenderProbability.HasValue)
            {
                query = query.Where(profile => profile.GenderProbability >= parameters.MinGenderProbability.Value);
            }

            if (parameters.MinCountryProbability.HasValue)
            {
                query = query.Where(profile => profile.CountryProbability >= parameters.MinCountryProbability.Value);
            }

            query = ApplySorting(query, parameters.SortBy, parameters.Order);

            var total = await query.CountAsync();
            var data = await query
                .Skip((parameters.Page - 1) * parameters.Limit)
                .Take(parameters.Limit)
                .ToListAsync();

            return new PagedProfilesResult
            {
                Page = parameters.Page,
                Limit = parameters.Limit,
                Total = total,
                Data = data
            };
        }

        private static IQueryable<Profile> ApplySorting(IQueryable<Profile> query, string? sortBy, string? order)
        {
            var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy) ? "created_at" : sortBy.Trim().ToLowerInvariant();
            var normalizedOrder = string.IsNullOrWhiteSpace(order) ? "asc" : order.Trim().ToLowerInvariant();
            var descending = normalizedOrder == "desc";

            return normalizedSortBy switch
            {
                "age" => descending ? query.OrderByDescending(profile => profile.Age).ThenBy(profile => profile.Name)
                    : query.OrderBy(profile => profile.Age).ThenBy(profile => profile.Name),
                "gender_probability" => descending ? query.OrderByDescending(profile => profile.GenderProbability).ThenBy(profile => profile.Name)
                    : query.OrderBy(profile => profile.GenderProbability).ThenBy(profile => profile.Name),
                _ => descending ? query.OrderByDescending(profile => profile.CreatedAt).ThenBy(profile => profile.Name)
                    : query.OrderBy(profile => profile.CreatedAt).ThenBy(profile => profile.Name)
            };
        }

        private static void ValidateQueryParameters(ProfileQueryParameters parameters)
        {
            var normalizedGender = parameters.Gender?.Trim().ToLowerInvariant();
            var normalizedAgeGroup = parameters.AgeGroup?.Trim().ToLowerInvariant();
            var normalizedCountryId = parameters.CountryId?.Trim().ToUpperInvariant();
            var normalizedSortBy = parameters.SortBy?.Trim().ToLowerInvariant();
            var normalizedOrder = parameters.Order?.Trim().ToLowerInvariant();

            if (parameters.Page < 1 || parameters.Limit < 1 || parameters.Limit > MaxLimit)
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }

            if (parameters.MinAge.HasValue && parameters.MinAge < 0)
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }

            if (parameters.MaxAge.HasValue && parameters.MaxAge < 0)
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }

            if (parameters.MinAge.HasValue && parameters.MaxAge.HasValue && parameters.MinAge > parameters.MaxAge)
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }

            if (!string.IsNullOrWhiteSpace(normalizedGender) &&
                normalizedGender is not ("male" or "female"))
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }

            if (!string.IsNullOrWhiteSpace(normalizedAgeGroup) &&
                normalizedAgeGroup is not ("child" or "teenager" or "adult" or "senior"))
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }

            if (!string.IsNullOrWhiteSpace(normalizedCountryId) &&
                (normalizedCountryId.Length != 2 || !normalizedCountryId.All(char.IsLetter)))
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }

            if (parameters.MinGenderProbability.HasValue &&
                (parameters.MinGenderProbability < 0 || parameters.MinGenderProbability > 1))
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }

            if (parameters.MinCountryProbability.HasValue &&
                (parameters.MinCountryProbability < 0 || parameters.MinCountryProbability > 1))
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }

            if (!string.IsNullOrWhiteSpace(normalizedSortBy) &&
                normalizedSortBy is not ("age" or "created_at" or "gender_probability"))
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }

            if (!string.IsNullOrWhiteSpace(normalizedOrder) &&
                normalizedOrder is not ("asc" or "desc"))
            {
                throw new InvalidQueryParametersException("Invalid query parameters");
            }
        }

        private static string ClassifyAgeGroup(int age)
        {
            if (age <= 12) return "child";
            if (age <= 19) return "teenager";
            if (age <= 59) return "adult";
            return "senior";
        }

        private static string ResolveCountryName(string countryCode)
        {
            try
            {
                return new System.Globalization.RegionInfo(countryCode.ToUpperInvariant()).EnglishName;
            }
            catch (ArgumentException)
            {
                return countryCode.ToUpperInvariant();
            }
        }
    }
}
