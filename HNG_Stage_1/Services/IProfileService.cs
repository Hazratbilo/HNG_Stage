using System.Collections.Generic;
using System.Threading.Tasks;
using HNG_Stage_1.Models;

namespace HNG_Stage_1.Services
{
    public interface IProfileService
    {
        Task<(Profile Profile, bool IsCreated)> CreateOrGetProfileAsync(string name);
        Task<Profile?> GetProfileByIdAsync(string id);
        Task<(int Count, List<Profile> Data)> GetAllProfilesAsync(string? gender, string? countryId, string? ageGroup);
        Task<bool> DeleteProfileAsync(string id);
    }
}
