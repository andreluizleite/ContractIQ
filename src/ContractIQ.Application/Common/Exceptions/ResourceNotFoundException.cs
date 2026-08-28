namespace ContractIQ.Application.Common.Exceptions;

public sealed class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string resourceName, object resourceId)
        : base($"{resourceName} '{resourceId}' was not found.")
    {
        ResourceName = resourceName;
        ResourceId = resourceId;
    }

    public string ResourceName { get; }

    public object ResourceId { get; }
}
