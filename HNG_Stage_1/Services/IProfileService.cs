using HNG_Stage_1.Models;

namespace HNG_Stage_1.Services
{
    public interface IProfileService
    {
        Task<(Profile Profile, bool IsCreated)> CreateOrGetProfileAsync(string name);
        Task<Profile?> GetProfileByIdAsync(string id);
        Task<PagedProfilesResult> GetProfilesAsync(ProfileQueryParameters parameters);
        Task<PagedProfilesResult> SearchProfilesAsync(string query, int page, int limit);
        Task<bool> DeleteProfileAsync(string id);
    }
}
