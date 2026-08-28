using ContractIQ.Application.Abstractions.Persistence;
using ContractIQ.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ContractIQ.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InMemoryDemoStore>();
        services.AddSingleton<ICustomerRepository>(provider =>
            provider.GetRequiredService<InMemoryDemoStore>());
        services.AddSingleton<IContractRepository>(provider =>
            provider.GetRequiredService<InMemoryDemoStore>());
        services.AddSingleton<ICancellationRequestStore>(provider =>
            provider.GetRequiredService<InMemoryDemoStore>());

        return services;
    }
}
