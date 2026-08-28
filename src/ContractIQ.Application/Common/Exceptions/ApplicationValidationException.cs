namespace ContractIQ.Application.Common.Exceptions;

public sealed class ApplicationValidationException : Exception
{
    public ApplicationValidationException(string field, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        Field = field;
    }

    public string Field { get; }
}
