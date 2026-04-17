using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HNG_Stage_1.Models
{
    public class Profile
    {
        [Key]
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("gender")]
        public string Gender { get; set; } = string.Empty;

        [JsonPropertyName("gender_probability")]
        public double GenderProbability { get; set; }

        [JsonPropertyName("sample_size")]
        public int SampleSize { get; set; }

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("age_group")]
        public string AgeGroup { get; set; } = string.Empty;

        [JsonPropertyName("country_id")]
        public string CountryId { get; set; } = string.Empty;

        [JsonPropertyName("country_probability")]
        public double CountryProbability { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
