using Microsoft.Extensions.Hosting;

namespace ContractIQ.Api.Security;

public static class LocalDemoEnvironmentGuard
{
    public static void EnsureSupported(string environmentName)
    {
        if (string.Equals(
                environmentName,
                Environments.Development,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidOperationException(
            "ContractIQ v1.0 allows anonymous access only in Development. " +
            "Do not expose this local demo publicly. Configure an authenticated " +
            "deployment profile before using Staging or Production.");
    }
}
