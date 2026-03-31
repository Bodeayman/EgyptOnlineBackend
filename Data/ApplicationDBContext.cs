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
        public DbSet<Sculptor> Sculptors { get; set; }

        // ─── Contract / Wallet / KYC Module ─────────────────────────
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<UserWallet> UserWallets { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<FundMovementLog> FundMovementLogs { get; set; }
        public DbSet<KycSubmission> KycSubmissions { get; set; }

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
            modelBuilder.Entity<Sculptor>().ToTable("Sculptors");

            // ─── Contract Module Configurations ─────────────────────
            modelBuilder.Entity<Contract>(entity =>
            {
                entity.ToTable("Contracts");
                entity.HasIndex(e => e.ContractorId);
                entity.HasIndex(e => e.EngineerId);
                entity.HasIndex(e => e.WorkerId);
                entity.HasIndex(e => e.Status);
                entity.HasOne(c => c.Contractor).WithMany().HasForeignKey(c => c.ContractorId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.Engineer).WithMany().HasForeignKey(c => c.EngineerId).OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(c => c.Worker).WithMany().HasForeignKey(c => c.WorkerId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserWallet>(entity =>
            {
                entity.ToTable("UserWallets");
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.HasOne(w => w.User).WithMany().HasForeignKey(w => w.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<WalletTransaction>(entity =>
            {
                entity.ToTable("WalletTransactions");
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.ContractId);
                entity.HasIndex(e => e.CreatedAt);
            });

            modelBuilder.Entity<AttendanceRecord>(entity =>
            {
                entity.ToTable("AttendanceRecords");
                entity.HasIndex(e => new { e.ContractId, e.Date }).IsUnique();
            });

            modelBuilder.Entity<FundMovementLog>(entity =>
            {
                entity.ToTable("FundMovementLogs");
                entity.HasIndex(e => e.ContractId);
            });

            modelBuilder.Entity<KycSubmission>(entity =>
            {
                entity.ToTable("KycSubmissions");
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);
            });
        }


    }
}