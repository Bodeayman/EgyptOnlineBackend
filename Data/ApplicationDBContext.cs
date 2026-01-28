using EgyptOnline.Models;
using Microsoft.AspNetCore.Identity;
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
        public DbSet<Assistant> Assistants { get; set; }
        public DbSet<Contractor> Contractors { get; set; }

        public DbSet<User> Users { get; set; }
        public DbSet<ServicesProvider> ServiceProviders { get; set; }

        public DbSet<Engineer> Engineers { get; set; }

        public DbSet<MarketPlace> MarketPlaces { get; set; }


        public DbSet<Subscription> Subscriptions { get; set; }

        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

        public DbSet<FirebaseToken> FirebaseTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // modelBuilder.Entity<User>().HasQueryFilter(u => !u.IsDeleted);
            modelBuilder.Entity<PaymentTransaction>()
                .HasIndex(p => p.IdempotencyKey)
                .IsUnique();
            // Repeat for other entities
            // modelBuilder.Entity<PaymentTransaction>().HasQueryFilter(p => !p.IsDeleted);
            // modelBuilder.Entity<ServiceProvider>().HasQueryFilter(s => !s.IsDeleted);
            modelBuilder.Entity<Contractor>().ToTable("Contractors");
            modelBuilder.Entity<ServicesProvider>().ToTable("ServicesProviders");
            modelBuilder.Entity<Company>().ToTable("Companies");
            modelBuilder.Entity<Worker>().ToTable("Workers");
            modelBuilder.Entity<MarketPlace>().ToTable("MarketPlaces");
            modelBuilder.Entity<Assistant>().ToTable("Assistants");
            modelBuilder.Entity<Engineer>().ToTable("Engineers");





        }


    }
}