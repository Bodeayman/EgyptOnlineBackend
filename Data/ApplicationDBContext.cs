using EgyptOnline.Models;
using EgyptOnline.Domain.Models;
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
        public DbSet<ContractDay> ContractDays { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<FundMovementLog> FundMovementLogs { get; set; }
        public DbSet<KycSubmission> KycSubmissions { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<DepositRequest> DepositRequests { get; set; }
        public DbSet<WithdrawRequest> WithdrawRequests { get; set; }
        public DbSet<JobRequest> JobRequests { get; set; }
        public DbSet<JobRequestInterest> JobRequestInterests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User unique constraints
            modelBuilder.Entity<User>(entity =>
            {
                // Email and UserName are already unique by Identity configuration
                // PhoneNumber unique only when not null (filtered index)
                // This allows multiple NULL values but prevents duplicate non-null phone numbers
                entity.HasIndex(e => e.PhoneNumber)
                    .HasFilter("\"PhoneNumber\" IS NOT NULL")
                    .IsUnique();
            });

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
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ClientUserId);
                entity.HasIndex(e => e.ServiceProviderPhoneNumber);

                entity.HasOne(c => c.ClientUser)
                    .WithMany()
                    .HasForeignKey(c => c.ClientUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<UserWallet>(entity =>
            {
                entity.ToTable("UserWallets");
                entity.HasIndex(e => e.UserId).IsUnique();
                entity.HasOne(w => w.User)
                      .WithOne(u => u.Wallet)
                      .HasForeignKey<UserWallet>(w => w.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ContractDay>(entity =>
            {
                entity.ToTable("ContractDays");
                entity.HasIndex(e => new { e.ContractId, e.DayNumber }).IsUnique();
                entity.HasIndex(e => new { e.ContractId, e.Date });
                entity.HasIndex(e => e.IsProcessed);
                entity.HasOne(cd => cd.Contract)
                      .WithMany(c => c.ContractDays)
                      .HasForeignKey(cd => cd.ContractId)
                      .OnDelete(DeleteBehavior.Cascade);
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

            modelBuilder.Entity<Complaint>(entity =>
            {
                entity.ToTable("Complaints");
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ContractId);
                entity.HasIndex(e => e.ReporterUserId);
                entity
                    .HasOne(c => c.Reporter)
                    .WithMany()
                    .HasForeignKey(c => c.ReporterUserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity
                    .HasOne(c => c.Contract)
                    .WithMany()
                    .HasForeignKey(c => c.ContractId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<DepositRequest>(entity =>
            {
                entity.ToTable("DepositRequests");
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);
            });

            modelBuilder.Entity<WithdrawRequest>(entity =>
            {
                entity.ToTable("WithdrawRequests");
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);
            });

            modelBuilder.Entity<JobRequest>(entity =>
            {
                entity.ToTable("JobRequests");
                entity.HasIndex(e => e.ClientUserId);
                entity.HasIndex(e => e.Governorate);
                entity.HasOne(r => r.ClientUser).WithMany().HasForeignKey(r => r.ClientUserId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(r => r.AcceptedProviderUser).WithMany().HasForeignKey(r => r.AcceptedProviderUserId).OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<JobRequestInterest>(entity =>
            {
                entity.ToTable("JobRequestInterests");
                entity.HasIndex(e => new { e.JobRequestId, e.ServiceProviderUserId }).IsUnique();
                entity.HasOne(i => i.JobRequest).WithMany(r => r.Interests).HasForeignKey(i => i.JobRequestId).OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(i => i.ServiceProviderUser).WithMany().HasForeignKey(i => i.ServiceProviderUserId).OnDelete(DeleteBehavior.Cascade);
            });
        }


    }
}