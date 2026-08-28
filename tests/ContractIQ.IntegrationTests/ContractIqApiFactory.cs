using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ContractIQ.IntegrationTests;

internal sealed class ContractIqApiFactory(
    DateTimeOffset utcNow,
    TimeZoneInfo? localTimeZone = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(
                new FrozenTimeProvider(utcNow, localTimeZone ?? TimeZoneInfo.Utc));
        });
    }

    private sealed class FrozenTimeProvider(
        DateTimeOffset utcNow,
        TimeZoneInfo localTimeZone) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => localTimeZone;

        public override DateTimeOffset GetUtcNow() => utcNow.ToUniversalTime();
    }
}
