using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
        [Column("gender")]
        public string Gender { get; set; } = string.Empty;

        [JsonPropertyName("gender_probability")]
        [Column("gender_probability")]
        public double GenderProbability { get; set; }

        [JsonPropertyName("country_name")]
        [Column("country_name")]
        public string CountryName { get; set; } = string.Empty;

        [JsonPropertyName("age")]
        [Column("age")]
        public int Age { get; set; }

        [JsonPropertyName("age_group")]
        [Column("age_group")]
        public string AgeGroup { get; set; } = string.Empty;

        [JsonPropertyName("country_id")]
        [Column("country_id")]
        [StringLength(2)]
        public string CountryId { get; set; } = string.Empty;

        [JsonPropertyName("country_probability")]
        [Column("country_probability")]
        public double CountryProbability { get; set; }

        [JsonPropertyName("created_at")]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
