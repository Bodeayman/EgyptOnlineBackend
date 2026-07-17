using EgyptOnline.Data;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Tests.Unit;

/// <summary>
/// Base class for all unit tests.
/// - Uses in-memory EF Core database (isolated per test).
/// - Exposes FakeItEasy helpers and a pre-wired UserManager fake.
/// </summary>
public abstract class UnitTestBase : IDisposable
{
    protected readonly ApplicationDbContext Context;
    protected readonly UserManager<EgyptOnline.Models.User> UserManagerFake;

    protected UnitTestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Context = new ApplicationDbContext(options);

        // Build a fully faked UserManager – avoids the awkward 9-arg constructor.
        var store = A.Fake<IUserStore<EgyptOnline.Models.User>>();
        UserManagerFake = A.Fake<UserManager<EgyptOnline.Models.User>>(
            x => x.WithArgumentsForConstructor(new object[]
            {
                store,
                null!, null!, null!, null!, null!, null!, null!, null!
            }));
    }

    public void Dispose() => Context.Dispose();
}
