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
            
            // To be safe with Name unqiueness, we could add an index but case-sensitivities vary.
            // Since requirements say 'If the same name comes in again... Return existing one',
            // we will handle it in the Service before inserting. It's a good practice to index it.
            modelBuilder.Entity<Profile>()
                .HasIndex(p => p.Name)
                .IsUnique();
        }
    }
}
