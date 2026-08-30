using System.Diagnostics;
using OpenTelemetry;

namespace ContractIQ.Api.Observability;

public sealed class HttpRoutePrivacyProcessor : BaseProcessor<Activity>
{
    public override void OnEnd(Activity activity)
    {
        if (activity.Kind != ActivityKind.Server)
        {
            return;
        }

        // ASP.NET Core's route template remains available through http.route.
        // Raw URL tags are removed because ContractIQ routes contain business IDs.
        activity.SetTag("url.path", null);
        activity.SetTag("url.full", null);
        activity.SetTag("http.target", null);
        activity.SetTag("http.url", null);
    }
}
