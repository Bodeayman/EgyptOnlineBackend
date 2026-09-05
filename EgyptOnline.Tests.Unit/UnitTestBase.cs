using EgyptOnline.Data;
using FakeItEasy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace EgyptOnline.Tests.Unit
{
public abstract class UnitTestBase : IDisposable
{
    protected readonly ApplicationDbContext Context;
    protected readonly UserManager<EgyptOnline.Models.User> UserManagerFake;
    protected readonly IDistributedCache Cache;

    protected UnitTestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        Context = new ApplicationDbContext(options);

        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        var serviceProvider = services.BuildServiceProvider();
        Cache = serviceProvider.GetRequiredService<IDistributedCache>();

        var store = A.Fake<IUserStore<EgyptOnline.Models.User>>();
        UserManagerFake = A.Fake<UserManager<EgyptOnline.Models.User>>(
            x => x.WithArgumentsForConstructor(new object[]
            {
                store,
                null!, null!, null!, null!, null!, null!, null!, null!
            }));
    }

    public void Dispose()
    {
        Context.Dispose();
    }
}
}
