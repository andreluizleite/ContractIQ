namespace ContractIQ.Application.Common.Exceptions;

public sealed class ExternalDependencyUnavailableException(
    string dependency,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string Dependency { get; } = dependency;
}
