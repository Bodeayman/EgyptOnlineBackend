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
using EgyptOnline.Dtos;

namespace EgyptOnline.Tests
{
    /// <summary>
    /// Provider Registration Tests - Test all entity types registration
    /// 
    /// FOCUS: Tests registering all service provider types (Worker, Assistant, Contractor, Company, MarketPlace, Engineer)
    /// 
    /// EXPLANATION FOR INTERNS:
    /// This test suite demonstrates:
    /// - Creating different entity types in the database
    /// - Validating entity-specific properties
    /// - Using in-memory database to test without external dependencies
    /// - The Arrange-Act-Assert pattern for clear test structure
    /// </summary>
    public class ProviderRegistrationTests
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<UserManager<User>> _userManagerMock;

        public ProviderRegistrationTests()
        {
            // Setup: Create a fresh in-memory database for each test
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            _context = new ApplicationDbContext(options);

            // Mock UserManager
            var store = new Mock<IUserStore<User>>();
            _userManagerMock = new Mock<UserManager<User>>(
                store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        // ===========================
        // SCENARIO 1: REGISTER WORKER
        // ===========================

        [Fact]
        public async Task RegisterWorker_WithValidData_ShouldCreateWorkerSuccessfully()
        {
            // ARRANGE - Create a user
            var user = new User
            {
                Id = "user_worker_123",
                UserName = "ahmed.worker",
                Email = "ahmed.worker@test.com",
                PhoneNumber = "+201001234567",
                FirstName = "Ahmed",
                LastName = "Worker",
                Governorate = "Cairo",
                City = "Cairo",
                Points = 100,
                SubscriptionPoints = 50
            };

            // Add subscription
            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(1)
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);

            // Create worker
            var worker = new Worker
            {
                Id = 1,
                UserId = user.Id,
                User = user,
                Skill = "Plumbing",
                WorkerType = WorkerTypes.PerDay,
                ProviderType = "worker",
                ServicePricePerDay = 500,
                Bio = "Experienced plumber",
                IsAvailable = true
            };

            _context.Workers.Add(worker);

            // ACT
            await _context.SaveChangesAsync();

            // ASSERT
            var savedWorker = await _context.Workers
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.Id == worker.Id);

            Assert.NotNull(savedWorker);
            Assert.Equal("Plumbing", savedWorker.Skill);
            Assert.Equal(500, savedWorker.ServicePricePerDay);
            Assert.Equal(WorkerTypes.PerDay, savedWorker.WorkerType);
            Assert.True(savedWorker.IsAvailable);
            Assert.Equal("ahmed.worker", savedWorker.User.UserName);
        }

        // =============================
        // SCENARIO 2: REGISTER ASSISTANT
        // =============================

        [Fact]
        public async Task RegisterAssistant_WithValidData_ShouldCreateAssistantSuccessfully()
        {
            // ARRANGE
            var user = new User
            {
                Id = "user_assistant_456",
                UserName = "fatima.assistant",
                Email = "fatima.assistant@test.com",
                PhoneNumber = "+201009876543",
                FirstName = "Fatima",
                LastName = "Assistant",
                Governorate = "Alexandria",
                City = "Alexandria",
                Points = 150,
                SubscriptionPoints = 75
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(3)
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);

            // Create assistant
            var assistant = new Assistant
            {
                Id = 2,
                UserId = user.Id,
                User = user,
                Skill = "Administrative Support",
                ProviderType = "assistant",
                ServicePricePerDay = 350,
                Bio = "Professional administrative assistant",
                IsAvailable = true
            };

            _context.Assistants.Add(assistant);

            // ACT
            await _context.SaveChangesAsync();

            // ASSERT
            var savedAssistant = await _context.Assistants
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == assistant.Id);

            Assert.NotNull(savedAssistant);
            Assert.Equal("Administrative Support", savedAssistant.Skill);
            Assert.Equal(350, savedAssistant.ServicePricePerDay);
            Assert.Equal("assistant", savedAssistant.ProviderType);
            Assert.Equal("fatima.assistant", savedAssistant.User.UserName);
        }

        // ==============================
        // SCENARIO 3: REGISTER CONTRACTOR
        // ==============================

        [Fact]
        public async Task RegisterContractor_WithValidData_ShouldCreateContractorSuccessfully()
        {
            // ARRANGE
            var user = new User
            {
                Id = "user_contractor_789",
                UserName = "omar.contractor",
                Email = "omar.contractor@test.com",
                PhoneNumber = "+201112223333",
                FirstName = "Omar",
                LastName = "Contractor",
                Governorate = "Giza",
                City = "Giza",
                Points = 200,
                SubscriptionPoints = 100
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6)
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);

            // Create contractor
            var contractor = new Contractor
            {
                Id = 3,
                UserId = user.Id,
                User = user,
                Specialization = "Civil Engineering",
                ProviderType = "contractor",
                Salary = 8000,
                Bio = "Licensed civil engineer",
                IsAvailable = true
            };

            _context.Contractors.Add(contractor);

            // ACT
            await _context.SaveChangesAsync();

            // ASSERT
            var savedContractor = await _context.Contractors
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == contractor.Id);

            Assert.NotNull(savedContractor);
            Assert.Equal("Civil Engineering", savedContractor.Specialization);
            Assert.Equal(8000, savedContractor.Salary);
            Assert.Equal("contractor", savedContractor.ProviderType);
        }

        // ===========================
        // SCENARIO 4: REGISTER COMPANY
        // ===========================

        [Fact]
        public async Task RegisterCompany_WithValidData_ShouldCreateCompanySuccessfully()
        {
            // ARRANGE
            var user = new User
            {
                Id = "user_company_001",
                UserName = "ali.company",
                Email = "ali.company@test.com",
                PhoneNumber = "+201445556666",
                FirstName = "Ali",
                LastName = "Company",
                Governorate = "Cairo",
                City = "New Cairo",
                Points = 500,
                SubscriptionPoints = 250
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(12)
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);

            // Create company
            var company = new Company
            {
                Id = 4,
                UserId = user.Id,
                User = user,
                Business = "Software Development",
                Owner = "Ali Ahmed",
                ProviderType = "company",
                Bio = "Leading software development company",
                IsAvailable = true
            };

            _context.Companies.Add(company);

            // ACT
            await _context.SaveChangesAsync();

            // ASSERT
            var savedCompany = await _context.Companies
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.Id == company.Id);

            Assert.NotNull(savedCompany);
            Assert.Equal("Software Development", savedCompany.Business);
            Assert.Equal("Ali Ahmed", savedCompany.Owner);
            Assert.Equal("company", savedCompany.ProviderType);
        }

        // ================================
        // SCENARIO 5: REGISTER MARKETPLACE
        // ================================

        [Fact]
        public async Task RegisterMarketPlace_WithValidData_ShouldCreateMarketPlaceSuccessfully()
        {
            // ARRANGE
            var user = new User
            {
                Id = "user_marketplace_002",
                UserName = "sara.marketplace",
                Email = "sara.marketplace@test.com",
                PhoneNumber = "+201778889999",
                FirstName = "Sara",
                LastName = "MarketPlace",
                Governorate = "Alexandria",
                City = "Alexandria",
                Points = 300,
                SubscriptionPoints = 150
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(3)
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);

            // Create marketplace
            var marketplace = new MarketPlace
            {
                Id = 5,
                UserId = user.Id,
                User = user,
                Business = "E-commerce Platform",
                Owner = "Sara Mohamed",
                ProviderType = "marketplace",
                Bio = "Online shopping marketplace",
                IsAvailable = true
            };

            _context.MarketPlaces.Add(marketplace);

            // ACT
            await _context.SaveChangesAsync();

            // ASSERT
            var savedMarketplace = await _context.MarketPlaces
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == marketplace.Id);

            Assert.NotNull(savedMarketplace);
            Assert.Equal("E-commerce Platform", savedMarketplace.Business);
            Assert.Equal("Sara Mohamed", savedMarketplace.Owner);
            Assert.Equal("marketplace", savedMarketplace.ProviderType);
        }

        // =============================
        // SCENARIO 6: REGISTER ENGINEER
        // =============================

        [Fact]
        public async Task RegisterEngineer_WithValidData_ShouldCreateEngineerSuccessfully()
        {
            // ARRANGE
            var user = new User
            {
                Id = "user_engineer_003",
                UserName = "mahmoud.engineer",
                Email = "mahmoud.engineer@test.com",
                PhoneNumber = "+201234445555",
                FirstName = "Mahmoud",
                LastName = "Engineer",
                Governorate = "Giza",
                City = "Giza",
                Points = 400,
                SubscriptionPoints = 200
            };

            var subscription = new Subscription
            {
                UserId = user.Id,
                User = user,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6)
            };

            _context.Users.Add(user);
            _context.Subscriptions.Add(subscription);

            // Create engineer
            var engineer = new Engineer
            {
                Id = 6,
                UserId = user.Id,
                User = user,
                Specialization = "Mechanical Engineering",
                ProviderType = "engineer",
                Salary = 12000,
                Bio = "Experienced mechanical engineer",
                IsAvailable = true
            };

            _context.Engineers.Add(engineer);

            // ACT
            await _context.SaveChangesAsync();

            // ASSERT
            var savedEngineer = await _context.Engineers
                .Include(e => e.User)
                .FirstOrDefaultAsync(e => e.Id == engineer.Id);

            Assert.NotNull(savedEngineer);
            Assert.Equal("Mechanical Engineering", savedEngineer.Specialization);
            Assert.Equal(12000, savedEngineer.Salary);
            Assert.Equal("engineer", savedEngineer.ProviderType);
        }

        // ========================================
        // SCENARIO 7: REGISTER ALL TYPES TOGETHER
        // ========================================

        [Fact]
        public async Task RegisterAllProviderTypes_ShouldCreateMultipleProvidersSuccessfully()
        {
            // ARRANGE - Create multiple users with different provider types
            var users = new List<User>
            {
                new User { Id = "worker_multi", UserName = "worker1", Email = "worker1@test.com", PhoneNumber = "+201001111111", FirstName = "W", LastName = "1", Governorate = "Cairo", City = "Cairo", Points = 100, SubscriptionPoints = 50 },
                new User { Id = "assistant_multi", UserName = "assistant1", Email = "assistant1@test.com", PhoneNumber = "+201002222222", FirstName = "A", LastName = "1", Governorate = "Cairo", City = "Cairo", Points = 100, SubscriptionPoints = 50 },
                new User { Id = "engineer_multi", UserName = "engineer1", Email = "engineer1@test.com", PhoneNumber = "+201003333333", FirstName = "E", LastName = "1", Governorate = "Cairo", City = "Cairo", Points = 100, SubscriptionPoints = 50 }
            };

            foreach (var user in users)
            {
                _context.Users.Add(user);
                _context.Subscriptions.Add(new Subscription
                {
                    UserId = user.Id,
                    User = user,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddMonths(1)
                });
            }

            var worker = new Worker { Id = 10, UserId = users[0].Id, User = users[0], Skill = "Skill1", WorkerType = WorkerTypes.PerDay, ProviderType = "worker", ServicePricePerDay = 500, IsAvailable = true };
            var assistant = new Assistant { Id = 11, UserId = users[1].Id, User = users[1], Skill = "Skill2", ProviderType = "assistant", ServicePricePerDay = 300, IsAvailable = true };
            var engineer = new Engineer { Id = 12, UserId = users[2].Id, User = users[2], Specialization = "Spec1", ProviderType = "engineer", Salary = 10000, IsAvailable = true };

            _context.Workers.Add(worker);
            _context.Assistants.Add(assistant);
            _context.Engineers.Add(engineer);

            // ACT
            await _context.SaveChangesAsync();

            // ASSERT
            var workerCount = await _context.Workers.CountAsync();
            var assistantCount = await _context.Assistants.CountAsync();
            var engineerCount = await _context.Engineers.CountAsync();

            Assert.Equal(1, workerCount);
            Assert.Equal(1, assistantCount);
            Assert.Equal(1, engineerCount);
        }
    }
}
