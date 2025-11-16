using EgyptOnline.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Data
{


    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }


        public DbSet<Worker> Workers { get; set; }
        public DbSet<Company> Companies { get; set; }

        public DbSet<Contractor> Contractors { get; set; }

        public DbSet<User> Users { get; set; }
        public DbSet<ServicesProvider> ServiceProviders { get; set; }

        public DbSet<Engineer> Engineers { get; set; }

        public DbSet<MarketPlace> MarketPlaces { get; set; }


        public DbSet<Subscription> Subscriptions { get; set; }

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Contractor>().ToTable("Contractors");
            modelBuilder.Entity<ServicesProvider>().ToTable("ServicesProviders");
            modelBuilder.Entity<Company>().ToTable("Companies");
            modelBuilder.Entity<Worker>().ToTable("Workers");
            modelBuilder.Entity<MarketPlace>().ToTable("MarketPlaces");

            modelBuilder.Entity<Engineer>().ToTable("Engineers");
            modelBuilder.Entity<Subscription>()
                  .Property(s => s.StartDate)
                  .HasColumnType("date");

            modelBuilder.Entity<Subscription>()
                .Property(s => s.EndDate)
                .HasColumnType("date");




        }


    }
}