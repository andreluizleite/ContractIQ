using ContractIQ.Application;
using Xunit;

namespace ContractIQ.Application.Tests;

public sealed class ApplicationAssemblyTests
{
    [Fact]
    public void Assembly_marker_is_available()
    {
        Assert.Equal("ContractIQ.Application", typeof(ApplicationAssembly).Assembly.GetName().Name);
    }
}
