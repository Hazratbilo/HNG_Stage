using HNG_Stage_1.Models;
using HNG_Stage_1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using CsvHelper;
using System.Globalization;

namespace HNG_Stage_1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AnyRole")]
    [EnableRateLimiting("ApiPolicy")]
    public class ProfilesController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfilesController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        public class CreateProfileRequest
        {
            public string? Name { get; set; }
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CreateProfile([FromBody] CreateProfileRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { status = "error", message = "Missing or empty name" });
            }

            var result = await _profileService.CreateOrGetProfileAsync(request.Name);
            if (result.IsCreated)
            {
                return Created($"/api/profiles/{result.Profile.Id}", new
                {
                    status = "success",
                    data = result.Profile
                });
            }

            return Ok(new
            {
                status = "success",
                message = "Profile already exists",
                data = result.Profile
            });
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchProfiles([FromQuery(Name = "q")] string? q, [FromQuery] int page = 1, [FromQuery] int limit = 10)
        {
            var result = await _profileService.SearchProfilesAsync(q ?? string.Empty, page, limit);
            var totalPages = result.Total == 0 ? 0 : (result.Total + result.Limit - 1) / result.Limit;
            
            return Ok(new
            {
                status = "success",
                page = result.Page,
                limit = result.Limit,
                total = result.Total,
                total_pages = totalPages,
                links = new
                {
                    self = $"/api/profiles/search?q={Uri.EscapeDataString(q ?? "")}&page={result.Page}&limit={result.Limit}",
                    next = result.Page < totalPages ? $"/api/profiles/search?q={Uri.EscapeDataString(q ?? "")}&page={result.Page + 1}&limit={result.Limit}" : null,
                    prev = result.Page > 1 ? $"/api/profiles/search?q={Uri.EscapeDataString(q ?? "")}&page={result.Page - 1}&limit={result.Limit}" : null
                },
                data = result.Data
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProfile(string id)
        {
            var profile = await _profileService.GetProfileByIdAsync(id);
            if (profile == null)
            {
                return NotFound(new { status = "error", message = "Profile not found" });
            }

            return Ok(new
            {
                status = "success",
                data = profile
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAllProfiles(
            [FromQuery(Name = "gender")] string? gender,
            [FromQuery(Name = "age_group")] string? ageGroup,
            [FromQuery(Name = "country_id")] string? countryId,
            [FromQuery(Name = "min_age")] int? minAge,
            [FromQuery(Name = "max_age")] int? maxAge,
            [FromQuery(Name = "min_gender_probability")] double? minGenderProbability,
            [FromQuery(Name = "min_country_probability")] double? minCountryProbability,
            [FromQuery(Name = "sort_by")] string? sortBy,
            [FromQuery(Name = "order")] string? order,
            [FromQuery(Name = "page")] int page = 1,
            [FromQuery(Name = "limit")] int limit = 10)
        {
            var result = await _profileService.GetProfilesAsync(new ProfileQueryParameters
            {
                Gender = gender,
                AgeGroup = ageGroup,
                CountryId = countryId,
                MinAge = minAge,
                MaxAge = maxAge,
                MinGenderProbability = minGenderProbability,
                MinCountryProbability = minCountryProbability,
                SortBy = sortBy,
                Order = order,
                Page = page,
                Limit = limit
            });

            var totalPages = result.Total == 0 ? 0 : (result.Total + result.Limit - 1) / result.Limit;
            var qs = Request.QueryString.HasValue ? Request.QueryString.Value : "";
            
            // Reconstruct logic for links could be complex with query strings, but simplified:
            string BuildLink(int p)
            {
                var dict = Request.Query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
                dict["page"] = p.ToString();
                dict["limit"] = limit.ToString();
                var queryParams = string.Join("&", dict.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
                return $"/api/profiles?{queryParams}";
            }

            return Ok(new
            {
                status = "success",
                page = result.Page,
                limit = result.Limit,
                total = result.Total,
                total_pages = totalPages,
                links = new
                {
                    self = BuildLink(result.Page),
                    next = result.Page < totalPages ? BuildLink(result.Page + 1) : null,
                    prev = result.Page > 1 ? BuildLink(result.Page - 1) : null
                },
                data = result.Data
            });
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteProfile(string id)
        {
            var success = await _profileService.DeleteProfileAsync(id);
            if (!success)
            {
                return NotFound(new { status = "error", message = "Profile not found" });
            }

            return NoContent();
        }

        [HttpGet("export")]
        public async Task<IActionResult> ExportProfiles(
            [FromQuery(Name = "format")] string format,
            [FromQuery(Name = "gender")] string? gender,
            [FromQuery(Name = "age_group")] string? ageGroup,
            [FromQuery(Name = "country_id")] string? countryId,
            [FromQuery(Name = "min_age")] int? minAge,
            [FromQuery(Name = "max_age")] int? maxAge,
            [FromQuery(Name = "min_gender_probability")] double? minGenderProbability,
            [FromQuery(Name = "min_country_probability")] double? minCountryProbability,
            [FromQuery(Name = "sort_by")] string? sortBy,
            [FromQuery(Name = "order")] string? order)
        {
            if (format?.ToLower() != "csv")
            {
                return BadRequest(new { status = "error", message = "Unsupported format" });
            }

            // Limit is maxed to a large number to export all that match
            var result = await _profileService.GetProfilesAsync(new ProfileQueryParameters
            {
                Gender = gender, AgeGroup = ageGroup, CountryId = countryId,
                MinAge = minAge, MaxAge = maxAge, MinGenderProbability = minGenderProbability,
                MinCountryProbability = minCountryProbability, SortBy = sortBy, Order = order,
                Page = 1, Limit = 50000 
            });

            var builder = new StringBuilder();
            using (var writer = new StringWriter(builder))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                // Specify columns order: id, name, gender, gender_probability, age, age_group, country_id, country_name, country_probability, created_at
                csv.WriteField("id");
                csv.WriteField("name");
                csv.WriteField("gender");
                csv.WriteField("gender_probability");
                csv.WriteField("age");
                csv.WriteField("age_group");
                csv.WriteField("country_id");
                csv.WriteField("country_name");
                csv.WriteField("country_probability");
                csv.WriteField("created_at");
                csv.NextRecord();

                foreach (var p in result.Data)
                {
                    csv.WriteField(p.Id);
                    csv.WriteField(p.Name);
                    csv.WriteField(p.Gender);
                    csv.WriteField(p.GenderProbability);
                    csv.WriteField(p.Age);
                    csv.WriteField(p.AgeGroup);
                    csv.WriteField(p.CountryId);
                    csv.WriteField(p.CountryName);
                    csv.WriteField(p.CountryProbability);
                    csv.WriteField(p.CreatedAt.ToString("o"));
                    csv.NextRecord();
                }
            }

            var bytes = Encoding.UTF8.GetBytes(builder.ToString());
            return File(bytes, "text/csv", $"profiles_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.csv");
        }
    }
}
