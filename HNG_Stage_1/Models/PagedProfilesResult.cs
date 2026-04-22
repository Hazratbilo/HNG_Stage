using System.Collections.Generic;

namespace HNG_Stage_1.Models
{
    public class PagedProfilesResult
    {
        public int Page { get; set; }
        public int Limit { get; set; }
        public int Total { get; set; }
        public IReadOnlyList<Profile> Data { get; set; } = new List<Profile>();
    }
}
