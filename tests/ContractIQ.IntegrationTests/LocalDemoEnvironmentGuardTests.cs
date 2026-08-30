using ContractIQ.Api.Security;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class LocalDemoEnvironmentGuardTests
{
    [Fact]
    public void Allows_the_local_development_environment()
    {
        LocalDemoEnvironmentGuard.EnsureSupported(Environments.Development);
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void Rejects_anonymous_startup_outside_development(string environmentName)
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LocalDemoEnvironmentGuard.EnsureSupported(environmentName));

        Assert.Contains("anonymous access only in Development", exception.Message);
    }
}
