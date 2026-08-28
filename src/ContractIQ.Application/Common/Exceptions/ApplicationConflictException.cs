namespace ContractIQ.Application.Common.Exceptions;

public sealed class ApplicationConflictException : Exception
{
    public ApplicationConflictException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Code = code;
    }

    public string Code { get; }
}
