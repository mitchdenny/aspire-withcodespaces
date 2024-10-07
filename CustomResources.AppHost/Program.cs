using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = DistributedApplication.CreateBuilder(args);

var custom = builder.AddCustomResource("custom");

var db = builder.AddPostgres("pg").AddDatabase("db")
                .WaitFor(custom);

var apiService = builder.AddProject<Projects.CustomResources_ApiService>("apiservice")
                        .WithReference(db);

builder.AddProject<Projects.CustomResources_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);


builder.Build().Run();

public class CustomResource(string name) : Resource(name)
{

}

public static class CustomResourceExtensions
{
    public static IResourceBuilder<CustomResource> AddCustomResource(this IDistributedApplicationBuilder builder, string name)
    {
        var resource = new CustomResource(name);

        // This is just to simulate some background process that is doing something with
        // this custom resource. in this case we just wait for 20 seconds
        bool customResourceIsHealthy = false;
        builder.Eventing.Subscribe<BeforeStartEvent>((@e, ct) => {
            _ = Task.Run(async () => {
                var rns = @e.Services.GetRequiredService<ResourceNotificationService>();

                await rns.PublishUpdateAsync(resource, s => s with {
                    State = "Starting"
                });

                await Task.Delay(20000);

                await rns.PublishUpdateAsync(resource, s => s with {
                    State = "Running"
                });

                customResourceIsHealthy = true;
            });
            return Task.CompletedTask;
        });

        var healthCheckKey = $"{name}_check";
        builder.Services.AddHealthChecks().AddCheck(healthCheckKey, (r) => customResourceIsHealthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy());
        
        return builder.AddResource(resource)
                      .WithHealthCheck(healthCheckKey);
    }
}