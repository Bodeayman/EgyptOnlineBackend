using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using EgyptOnline.Services;
using EgyptOnline.Data;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using System.Threading.Tasks;
using System;
using EgyptOnline.Domain.Interfaces;

namespace EgyptOnline.Tests
{
    /// <summary>
    /// Provider Search Tests - Test search functionality after login
    /// 
    /// FOCUS: Tests searching for service providers and verifying search results are not empty
    /// Includes seeding providers to in-memory database and testing search queries
    /// 
    /// KEY CONCEPTS DEMONSTRATED:
    /// - In-memory database seeding
    /// - LINQ queries for search functionality
    /// - Testing that search results contain expected data
    /// - Filtering by provider type and other criteria
    /// </summary>
    public class ProviderSearchTests
    {
        private readonly ApplicationDbContext _context;

        public ProviderSearchTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);
        }

        // ======================================
        // HELPER METHOD: Seed providers to database
        // ======================================

        private async Task SeedProvidersToDatabase()
        {
            // Create test users
            var user1 = new User
            {
                Id = "search_user_1",
                UserName = "hassan.worker",
                Email = "hassan.worker@test.com",
                PhoneNumber = "+201001111111",
                FirstName = "Hassan",
                LastName = "Worker",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100,
                SubscriptionPoints = 50
            };

            var user2 = new User
            {
                Id = "search_user_2",
                UserName = "layla.assistant",
                Email = "layla.assistant@test.com",
                PhoneNumber = "+201002222222",
                FirstName = "Layla",
                LastName = "Assistant",
                Governorate = "Alexandria",
                City = "Alexandria",
                Points = 150,
                SubscriptionPoints = 75
            };

            var user3 = new User
            {
                Id = "search_user_3",
                UserName = "khaled.engineer",
                Email = "khaled.engineer@test.com",
                PhoneNumber = "+201003333333",
                FirstName = "Khaled",
                LastName = "Engineer",
                Governorate = "Giza",
                City = "Giza",
                Points = 200,
                SubscriptionPoints = 100
            };

            var user4 = new User
            {
                Id = "search_user_4",
                UserName = "noor.contractor",
                Email = "noor.contractor@test.com",
                PhoneNumber = "+201004444444",
                FirstName = "Noor",
                LastName = "Contractor",
                Governorate = "Cairo",
                City = "Helwan",
                Points = 120,
                SubscriptionPoints = 60
            };

            _context.Users.AddRange(user1, user2, user3, user4);

            // Add subscriptions
            var subscriptions = new List<Subscription>
            {
                new Subscription { UserId = user1.Id, User = user1, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(1) },
                new Subscription { UserId = user2.Id, User = user2, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(2) },
                new Subscription { UserId = user3.Id, User = user3, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(3) },
                new Subscription { UserId = user4.Id, User = user4, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(1) }
            };
            _context.Subscriptions.AddRange(subscriptions);

            // Create workers
            var worker1 = new Worker
            {
                Id = 1,
                UserId = user1.Id,
                User = user1,
                Skill = "Plumbing",
                WorkerType = WorkerTypes.PerDay,
                ProviderType = "worker",
                ServicePricePerDay = 500,
                Bio = "Expert plumber with 10 years experience",
                IsAvailable = true
            };

            var worker2 = new Worker
            {
                Id = 2,
                UserId = "search_user_5",
                User = new User
                {
                    Id = "search_user_5",
                    UserName = "ahmed.plumber",
                    Email = "ahmed.plumber@test.com",
                    PhoneNumber = "+201005555555",
                    FirstName = "Ahmed",
                    LastName = "Plumber",
                    Governorate = "Giza",
                    City = "Giza",
                    Points = 80,
                    SubscriptionPoints = 40
                },
                Skill = "Electrical Work",
                WorkerType = WorkerTypes.PerPay,
                ProviderType = "worker",
                ServicePricePerDay = 450,
                Bio = "Licensed electrician",
                IsAvailable = true
            };

            _context.Users.Add(worker2.User);
            _context.Subscriptions.Add(new Subscription { UserId = worker2.UserId, User = worker2.User, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(1) });

            // Create assistants
            var assistant1 = new Assistant
            {
                Id = 3,
                UserId = user2.Id,
                User = user2,
                Skill = "Administrative Support",
                ProviderType = "assistant",
                ServicePricePerDay = 350,
                Bio = "Highly organized administrative professional",
                IsAvailable = true
            };

            // Create engineers
            var engineer1 = new Engineer
            {
                Id = 4,
                UserId = user3.Id,
                User = user3,
                Specialization = "Civil Engineering",
                ProviderType = "engineer",
                Salary = 15000,
                Bio = "Senior civil engineer",
                IsAvailable = true
            };

            // Create contractors
            var contractor1 = new Contractor
            {
                Id = 5,
                UserId = user4.Id,
                User = user4,
                Specialization = "Construction",
                ProviderType = "contractor",
                Salary = 12000,
                Bio = "General contractor",
                IsAvailable = true
            };

            _context.Workers.AddRange(worker1, worker2);
            _context.Assistants.Add(assistant1);
            _context.Engineers.Add(engineer1);
            _context.Contractors.Add(contractor1);

            await _context.SaveChangesAsync();
        }

        // =============================================
        // SCENARIO 1: SEARCH ALL WORKERS - Not Empty
        // =============================================

        [Fact]
        public async Task SearchWorkers_ShouldReturnNonEmptyResults()
        {
            // ARRANGE - Seed data
            await SeedProvidersToDatabase();

            // ACT - Search for all workers
            var workers = await _context.Workers
                .Include(w => w.User)
                .Where(w => w.IsAvailable)
                .ToListAsync();

            // ASSERT
            Assert.NotEmpty(workers); // Key assertion: Results must not be empty
            Assert.True(workers.Count >= 2); // We seeded at least 2 workers
            Assert.All(workers, w => Assert.NotNull(w.User));
            Assert.All(workers, w => Assert.True(w.IsAvailable));
        }

        // =====================================================
        // SCENARIO 2: SEARCH WORKERS BY SKILL - Not Empty
        // =====================================================

        [Fact]
        public async Task SearchWorkersBySkill_ShouldReturnNonEmptyResults()
        {
            // ARRANGE
            await SeedProvidersToDatabase();

            // ACT - Search for plumbers specifically
            var plumbers = await _context.Workers
                .Include(w => w.User)
                .Where(w => w.Skill.Contains("Plumbing") && w.IsAvailable)
                .ToListAsync();

            // ASSERT
            Assert.NotEmpty(plumbers); // Key assertion: Must find plumbers
            Assert.True(plumbers.Count > 0);
            Assert.All(plumbers, w => Assert.Contains("Plumbing", w.Skill));
        }

        // =============================================
        // SCENARIO 3: SEARCH ASSISTANTS - Not Empty
        // =============================================

        [Fact]
        public async Task SearchAssistants_ShouldReturnNonEmptyResults()
        {
            // ARRANGE
            await SeedProvidersToDatabase();

            // ACT - Search for all available assistants
            var assistants = await _context.Assistants
                .Include(a => a.User)
                .Where(a => a.IsAvailable)
                .ToListAsync();

            // ASSERT
            Assert.NotEmpty(assistants);
            Assert.True(assistants.Count > 0);
            Assert.All(assistants, a => Assert.NotNull(a.Skill));
        }

        // =============================================
        // SCENARIO 4: SEARCH ENGINEERS - Not Empty
        // =============================================

        [Fact]
        public async Task SearchEngineers_ShouldReturnNonEmptyResults()
        {
            // ARRANGE
            await SeedProvidersToDatabase();

            // ACT - Search for all available engineers
            var engineers = await _context.Engineers
                .Include(e => e.User)
                .Where(e => e.IsAvailable)
                .ToListAsync();

            // ASSERT
            Assert.NotEmpty(engineers);
            Assert.True(engineers.Count > 0);
            Assert.All(engineers, e => Assert.NotNull(e.Specialization));
        }

        // ===============================================
        // SCENARIO 5: SEARCH BY GOVERNORATE - Not Empty
        // ===============================================

        [Fact]
        public async Task SearchProvidersByGovernorate_ShouldReturnNonEmptyResults()
        {
            // ARRANGE
            await SeedProvidersToDatabase();

            // ACT - Search for providers in Cairo
            var cairoProviders = await _context.Workers
                .Include(w => w.User)
                .Where(w => w.User.Governorate == "Cairo" && w.IsAvailable)
                .ToListAsync();

            // ASSERT
            Assert.NotEmpty(cairoProviders);
            Assert.True(cairoProviders.Count > 0);
            Assert.All(cairoProviders, w => Assert.Equal("Cairo", w.User.Governorate));
        }

        // =================================================
        // SCENARIO 6: SEARCH BY PRICE RANGE - Not Empty
        // =================================================

        [Fact]
        public async Task SearchWorkersByPriceRange_ShouldReturnNonEmptyResults()
        {
            // ARRANGE
            await SeedProvidersToDatabase();

            // ACT - Search for workers with price between 400 and 600
            var affordableWorkers = await _context.Workers
                .Include(w => w.User)
                .Where(w => w.ServicePricePerDay >= 400 && w.ServicePricePerDay <= 600 && w.IsAvailable)
                .ToListAsync();

            // ASSERT
            Assert.NotEmpty(affordableWorkers);
            Assert.True(affordableWorkers.Count > 0);
            Assert.All(affordableWorkers, w => Assert.InRange(w.ServicePricePerDay, 400, 600));
        }

        // ======================================================
        // SCENARIO 7: SEARCH CONTRACTORS - Not Empty
        // ======================================================

        [Fact]
        public async Task SearchContractors_ShouldReturnNonEmptyResults()
        {
            // ARRANGE
            await SeedProvidersToDatabase();

            // ACT
            var contractors = await _context.Contractors
                .Include(c => c.User)
                .Where(c => c.IsAvailable)
                .ToListAsync();

            // ASSERT
            Assert.NotEmpty(contractors);
            Assert.True(contractors.Count > 0);
        }

        // ============================================================
        // SCENARIO 8: SEARCH WITH MULTIPLE FILTERS - Not Empty
        // ============================================================

        [Fact]
        public async Task SearchWithMultipleFilters_ShouldReturnNonEmptyResults()
        {
            // ARRANGE
            await SeedProvidersToDatabase();

            // ACT - Search for available workers in Giza with specific price
            var filteredWorkers = await _context.Workers
                .Include(w => w.User)
                .Where(w => w.User.Governorate == "Giza"
                    && w.IsAvailable
                    && w.ServicePricePerDay >= 400)
                .ToListAsync();

            // ASSERT
            Assert.NotEmpty(filteredWorkers);
            Assert.All(filteredWorkers, w => Assert.Equal("Giza", w.User.Governorate));
            Assert.All(filteredWorkers, w => Assert.True(w.IsAvailable));
            Assert.All(filteredWorkers, w => Assert.True(w.ServicePricePerDay >= 400));
        }

        // ===================================================
        // SCENARIO 9: SEARCH ALL PROVIDERS COUNT - Not Zero
        // ===================================================

        [Fact]
        public async Task GetAllProvidersCount_ShouldBeGreaterThanZero()
        {
            // ARRANGE
            await SeedProvidersToDatabase();

            // ACT
            var totalWorkers = await _context.Workers.CountAsync();
            var totalAssistants = await _context.Assistants.CountAsync();
            var totalEngineers = await _context.Engineers.CountAsync();
            var totalContractors = await _context.Contractors.CountAsync();

            var totalProviders = totalWorkers + totalAssistants + totalEngineers + totalContractors;

            // ASSERT
            Assert.True(totalWorkers > 0, "Workers count should be greater than 0");
            Assert.True(totalAssistants > 0, "Assistants count should be greater than 0");
            Assert.True(totalEngineers > 0, "Engineers count should be greater than 0");
            Assert.True(totalContractors > 0, "Contractors count should be greater than 0");
            Assert.True(totalProviders > 0, "Total providers count should be greater than 0");
        }
    }
}
