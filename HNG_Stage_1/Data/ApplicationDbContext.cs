using HNG_Stage_1.Models;
using Microsoft.EntityFrameworkCore;

namespace HNG_Stage_1.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Profile> Profiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var profile = modelBuilder.Entity<Profile>();
            profile.ToTable("profiles");

            profile.HasKey(p => p.Id);
            profile.Property(p => p.Id).HasColumnName("id");
            profile.Property(p => p.Name).HasColumnName("name").IsRequired();
            profile.Property(p => p.Gender).HasColumnName("gender").IsRequired();
            profile.Property(p => p.GenderProbability).HasColumnName("gender_probability");
            profile.Property(p => p.Age).HasColumnName("age");
            profile.Property(p => p.AgeGroup).HasColumnName("age_group").IsRequired();
            profile.Property(p => p.CountryId).HasColumnName("country_id").HasMaxLength(2).IsRequired();
            profile.Property(p => p.CountryName).HasColumnName("country_name").IsRequired();
            profile.Property(p => p.CountryProbability).HasColumnName("country_probability");
            profile.Property(p => p.CreatedAt).HasColumnName("created_at");

            profile.HasIndex(p => p.Name).IsUnique();
            profile.HasIndex(p => new { p.Gender, p.AgeGroup, p.CountryId });
            profile.HasIndex(p => p.Age);
            profile.HasIndex(p => p.CreatedAt);
        }
    }
}
