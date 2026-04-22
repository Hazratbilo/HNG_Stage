using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace HNG_Stage_1.Models
{
    public class ProfileQueryParameters
    {
        public string? Gender { get; set; }
        public string? AgeGroup { get; set; }
        public string? CountryId { get; set; }
        public int? MinAge { get; set; }
        public int? MaxAge { get; set; }
        public double? MinGenderProbability { get; set; }
        public double? MinCountryProbability { get; set; }
        public string? SortBy { get; set; }
        public string? Order { get; set; }
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;
    }

    public class SearchProfilesRequest
    {
        [FromQuery(Name = "q")]
        public string? Query { get; set; }

        [FromQuery(Name = "page")]
        public int Page { get; set; } = 1;

        [FromQuery(Name = "limit")]
        public int Limit { get; set; } = 10;
    }
}
