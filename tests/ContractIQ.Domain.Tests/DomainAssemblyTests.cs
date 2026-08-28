using ContractIQ.Domain;
using Xunit;

namespace ContractIQ.Domain.Tests;

public sealed class DomainAssemblyTests
{
    [Fact]
    public void Assembly_marker_is_available()
    {
        Assert.Equal("ContractIQ.Domain", typeof(DomainAssembly).Assembly.GetName().Name);
    }
}
