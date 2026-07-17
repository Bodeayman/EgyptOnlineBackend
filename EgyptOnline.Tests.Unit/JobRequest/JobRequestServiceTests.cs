using EgyptOnline.Application.Services.JobRequest;
using EgyptOnline.Models;
using EgyptOnline.Services;
using EgyptOnline.Utilities;
using FakeItEasy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EgyptOnline.Tests.Unit.JobRequest;

/// <summary>
/// Unit tests for JobRequestService.
///
/// Key invariants tested:
/// - After creation the job request exists in DB with Pending status.
/// - Notifications are fire-and-forget — they must NOT cause creation to fail.
/// - Only Pending requests can be cancelled.
/// - A completed request cannot be cancelled or completed again.
/// - Deleting a request that belongs to another user is forbidden.
/// </summary>
[Trait("Category", "Unit")]
public class JobRequestServiceTests : UnitTestBase
{
    private readonly INotificationService _notifFake = A.Fake<INotificationService>();
    private readonly OccupationService _occupationFake = A.Fake<OccupationService>();

    private JobRequestService BuildService() =>
        new(Context, _notifFake, _occupationFake);

    private async Task<User> SeedUser(string id = "client-1")
    {
        var user = new User
        {
            Id = id,
            UserName = $"user_{id}",
            PhoneNumber = "+201001234567",
            Governorate = "Cairo",
            City = "Cairo"
        };
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    // ── Creation ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateRequest_Succeeds_AndRequestIsPersistentWithPendingStatus()
    {
        await SeedUser("c1");
        var svc = BuildService();

        var result = await svc.CreateRequestAsync(
            clientUserId: "c1",
            providerType: "Worker",
            skill: "كهربائي",
            governorate: "Cairo",
            city: "Cairo",
            district: null,
            workDetails: null,
            workerPlace: null,
            perpayDetails: null,
            workerType: WorkerTypes.PerDay,
            payRate: 500,
            days: 3);

        // Persisted
        Assert.True(result.Id > 0);
        Assert.Equal("Pending", result.Status);

        var inDb = await Context.JobRequests.FindAsync(result.Id);
        Assert.NotNull(inDb);
        Assert.Equal("c1", inDb.ClientUserId);
    }

    [Fact]
    public async Task CreateRequest_WhenNotificationServiceFails_RequestIsStillCreated()
    {
        // Arrange – notification service throws on every call
        await SeedUser("c2");
        A.CallTo(() => _notifFake.SendNotificationToUser(
                A<string>._, A<string>._, A<string>._, A<string>._, A<string>._, A<string>._))
            .Throws<Exception>();

        var svc = BuildService();

        // Act – should NOT throw even if notif fails
        var result = await svc.CreateRequestAsync(
            "c2", "Engineer", "مهندس مدني", "Giza", "Giza",
            null, null, null, null, WorkerTypes.PerDay, 1000, 5);

        // Assert – request still in DB
        Assert.True(result.Id > 0);
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelRequest_OnPendingRequest_ChangesStatusToCancelled()
    {
        await SeedUser("c3");
        var svc = BuildService();

        var created = await svc.CreateRequestAsync(
            "c3", "Sculptor", "نحات", "Alexandria", "Alexandria",
            null, null, null, null, WorkerTypes.PerDay, 300, 1);

        var cancelled = await svc.CancelRequestAsync(created.Id, "c3");

        Assert.Equal("Cancelled", cancelled.Status);
        var inDb = await Context.JobRequests.FindAsync(created.Id);
        Assert.Equal("Cancelled", inDb!.Status);
    }

    [Fact]
    public async Task CancelRequest_AlreadyCancelled_ThrowsInvalidOperation()
    {
        await SeedUser("c4");
        var svc = BuildService();

        var created = await svc.CreateRequestAsync(
            "c4", "Contractor", "مقاول", "Cairo", "Cairo",
            null, null, null, null, WorkerTypes.PerDay, 5000, 10);

        await svc.CancelRequestAsync(created.Id, "c4");

        // Second cancel attempt
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CancelRequestAsync(created.Id, "c4"));
    }

    // ── Complete ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteRequest_AlreadyCompleted_ThrowsInvalidOperation()
    {
        await SeedUser("c5");
        var svc = BuildService();

        var created = await svc.CreateRequestAsync(
            "c5", "Worker", "عامل بناء", "Cairo", "Helwan",
            null, null, null, null, WorkerTypes.PerDay, 400, 2);

        await svc.CompleteRequestAsync(created.Id, "c5");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CompleteRequestAsync(created.Id, "c5"));
    }

    [Fact]
    public async Task CompleteRequest_OnCancelledRequest_ThrowsInvalidOperation()
    {
        await SeedUser("c6");
        var svc = BuildService();

        var created = await svc.CreateRequestAsync(
            "c6", "Worker", "سباك", "Cairo", "Maadi",
            null, null, null, null, WorkerTypes.PerDay, 350, 1);

        await svc.CancelRequestAsync(created.Id, "c6");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.CompleteRequestAsync(created.Id, "c6"));
    }

    // ── Delete Ownership ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteRequest_ByDifferentUser_ThrowsKeyNotFoundException()
    {
        await SeedUser("owner");
        await SeedUser("attacker");
        var svc = BuildService();

        var created = await svc.CreateRequestAsync(
            "owner", "Company", "شركة مقاولات", "Cairo", "Cairo",
            null, null, null, null, WorkerTypes.PerDay, 20000, 30);

        // Different user tries to delete
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.DeleteRequestAsync(created.Id, "attacker"));

        // Request still exists
        var inDb = await Context.JobRequests.FindAsync(created.Id);
        Assert.NotNull(inDb);
    }

    // ── Interest: cannot interest own request ─────────────────────────────────

    [Fact]
    public async Task SetInterest_OnOwnRequest_ThrowsUnauthorizedAccess()
    {
        await SeedUser("owner2");
        var svc = BuildService();

        var created = await svc.CreateRequestAsync(
            "owner2", "Worker", "كهربائي", "Cairo", "Cairo",
            null, null, null, null, WorkerTypes.PerDay, 400, 1);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.SetInterestAsync(created.Id, "owner2", true));
    }

    [Fact]
    public async Task SetInterest_OnCancelledRequest_ThrowsInvalidOperation()
    {
        await SeedUser("owner3");
        await SeedUser("provider3");
        var svc = BuildService();

        var created = await svc.CreateRequestAsync(
            "owner3", "Worker", "نجار", "Cairo", "Cairo",
            null, null, null, null, WorkerTypes.PerDay, 300, 1);

        await svc.CancelRequestAsync(created.Id, "owner3");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.SetInterestAsync(created.Id, "provider3", true));
    }
}
