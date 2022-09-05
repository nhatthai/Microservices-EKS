using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Diagnostics;

namespace NET6.Microservice.Core.OpenTelemetry
{
    public class OpenTelemetryStartup
    {
        public static void InitOpenTelemetryTracing(IServiceCollection services, IConfiguration configuration, string serviceName, string[] sources, IWebHostEnvironment webHostEnvironment = null)
        {
            services.AddOpenTelemetryTracing(builder =>
            {
                builder
                    .AddSource(serviceName)
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Enrich = Enrich;
                        options.RecordException = true;
                    })
                    .AddHttpClientInstrumentation()
                    .AddZipkinExporter(options =>
                    {
                        var zipkinURI = configuration.GetValue<string>("OpenTelemetry:ZipkinURI");
                        if (!string.IsNullOrEmpty(zipkinURI))
                        {
                            options.Endpoint = new Uri(zipkinURI);
                        }
                    })
                    .AddJaegerExporter(options =>
                    {
                        var agentHost = configuration.GetValue<string>("OpenTelemetry:JaegerHost");
                        var agentPort = configuration.GetValue<int>("OpenTelemetry:JaegerPort");

                        if (!string.IsNullOrEmpty(agentHost) && agentPort > 0)
                        {
                            options.AgentHost = agentHost;
                            options.AgentPort = agentPort;
                        }
                    });

                if (sources.Any())
                {
                    builder.AddSource(sources);
                }

                if (webHostEnvironment != null && webHostEnvironment.IsDevelopment())
                {
                    builder.AddConsoleExporter();
                }
            });

        }

        private static void Enrich(Activity activity, string eventName, object obj)
        {
            if (obj is HttpRequest request)
            {
                var context = request.HttpContext;
                activity.AddTag("http.scheme", request.Scheme);
                activity.AddTag("http.client_ip", context.Connection.RemoteIpAddress);
                activity.AddTag("http.request_content_length", request.ContentLength);
                activity.AddTag("http.request_content_type", request.ContentType);
            }
            else if (obj is HttpResponse response)
            {
                activity.AddTag("http.response_content_length", response.ContentLength);
                activity.AddTag("http.response_content_type", response.ContentType);
            }
        }
    }
}