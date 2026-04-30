using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using UUIDNext;

namespace HNG_Stage_1.Models
{
    public class User
    {
        [Key]
        [JsonPropertyName("id")]
        public string Id { get; set; } = Uuid.NewSequential().ToString();

        [JsonPropertyName("github_id")]
        [Column("github_id")]
        public string GithubId { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        [Column("email")]
        public string? Email { get; set; }

        [JsonPropertyName("avatar_url")]
        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("role")]
        [Column("role")]
        public string Role { get; set; } = "analyst"; // admin or analyst

        [JsonPropertyName("is_active")]
        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("last_login_at")]
        [Column("last_login_at")]
        public DateTime? LastLoginAt { get; set; }

        [JsonPropertyName("created_at")]
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
