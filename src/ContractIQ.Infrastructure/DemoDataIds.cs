namespace ContractIQ.Infrastructure;

/// <summary>
/// Stable identifiers for the fictional records used by the local demo experience.
/// </summary>
public static class DemoDataIds
{
    public static readonly Guid AcmeCustomer = new("11111111-1111-4111-8111-111111111111");

    public static readonly Guid GlobexCustomer = new("22222222-2222-4222-8222-222222222222");

    public static readonly Guid InitechCustomer = new("33333333-3333-4333-8333-333333333333");

    public static readonly Guid AcmeActiveContract = new("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    public static readonly Guid GlobexActiveContract = new("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

    public static readonly Guid InitechCancelledContract = new("cccccccc-cccc-4ccc-8ccc-cccccccccccc");

    public static readonly Guid InitechExpiredContract = new("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
}
