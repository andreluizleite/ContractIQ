namespace ContractIQ.Domain.Customers;

public sealed record Customer
{
    public Customer(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A customer identifier is required.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = id;
        Name = name.Trim();
    }

    public Guid Id { get; }

    public string Name { get; }
}
