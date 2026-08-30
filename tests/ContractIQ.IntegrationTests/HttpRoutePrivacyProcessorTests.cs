using System.Diagnostics;
using ContractIQ.Api.Observability;
using Xunit;

namespace ContractIQ.IntegrationTests;

public sealed class HttpRoutePrivacyProcessorTests
{
    [Fact]
    public void Removes_raw_server_paths_and_preserves_the_route_template()
    {
        using var source = new ActivitySource(nameof(HttpRoutePrivacyProcessorTests));
        using var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate.Name == source.Name,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using Activity activity = source.StartActivity(
            "http-request",
            ActivityKind.Server)!;
        activity.SetTag("url.path", "/api/v1/contracts/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        activity.SetTag("url.full", "https://localhost/api/v1/contracts/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        activity.SetTag("http.route", "/api/v1/contracts/{contractId:guid}");

        new HttpRoutePrivacyProcessor().OnEnd(activity);

        Assert.Null(activity.GetTagItem("url.path"));
        Assert.Null(activity.GetTagItem("url.full"));
        Assert.Equal(
            "/api/v1/contracts/{contractId:guid}",
            activity.GetTagItem("http.route"));
    }
}
