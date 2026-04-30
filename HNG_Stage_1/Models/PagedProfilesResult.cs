using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HNG_Stage_1.Models
{
    public class PagedProfilesResult
    {
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Total { get; set; }
        
        [JsonPropertyName("total_pages")]
        public int TotalPages => Total == 0 ? 0 : (Total + Limit - 1) / Limit;
        
        public PaginationLinks Links { get; set; } = new PaginationLinks();
        
        public IReadOnlyList<Profile> Data { get; set; } = new List<Profile>();
    }

    public class PaginationLinks
    {
        public string Self { get; set; } = string.Empty;
        public string? Next { get; set; }
        public string? Prev { get; set; }
    }
}
