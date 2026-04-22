using HNG_Stage_1.Models;
using HNG_Stage_1.Services;
using Microsoft.AspNetCore.Mvc;

namespace HNG_Stage_1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
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
            return Ok(new
            {
                status = "success",
                page = result.Page,
                limit = result.Limit,
                total = result.Total,
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

            return Ok(new
            {
                status = "success",
                page = result.Page,
                limit = result.Limit,
                total = result.Total,
                data = result.Data
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProfile(string id)
        {
            var success = await _profileService.DeleteProfileAsync(id);
            if (!success)
            {
                return NotFound(new { status = "error", message = "Profile not found" });
            }

            return NoContent();
        }
    }
}
