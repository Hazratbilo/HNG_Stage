using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.Linq;
using HNG_Stage_1.Services;
using HNG_Stage_1.Models;

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
            public string? name { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> CreateProfile([FromBody] CreateProfileRequest? request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.name))
            {
                return BadRequest(new { status = "error", message = "Missing or empty name" });
            }

            try
            {
                var result = await _profileService.CreateOrGetProfileAsync(request.name);

                if (result.IsCreated)
                {
                    return Created($"/api/profiles/{result.Profile.Id}", new
                    {
                        status = "success",
                        data = result.Profile
                    });
                }
                else
                {
                    return Ok(new
                    {
                        status = "success",
                        message = "Profile already exists",
                        data = result.Profile
                    });
                }
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { status = "error", message = ex.Message });
            }
            catch (ExternalApiException ex)
            {
                // This means one of the API calls returned invalid response or failed
                return StatusCode(502, new { status = "error", message = ex.Message });
            }
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
        public async Task<IActionResult> GetAllProfiles([FromQuery] string? gender, [FromQuery] string? country_id, [FromQuery] string? age_group)
        {
            var (count, data) = await _profileService.GetAllProfilesAsync(gender, country_id, age_group);

            return Ok(new
            {
                status = "success",
                count = count,
                data = data.Select(p => new {
                    id = p.Id,
                    name = p.Name,
                    gender = p.Gender,
                    age = p.Age,
                    age_group = p.AgeGroup,
                    country_id = p.CountryId
                })
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
