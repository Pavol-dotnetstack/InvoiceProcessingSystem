namespace InvoiceSystem.WebAPI.Extensions;

public static class CorsServiceExtensions
{
    public const string PolicyName = "AngularFrontend";

    public static IServiceCollection AddCustomCors(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();;

        // Fail-Fast Validation
        if (allowedOrigins == null || allowedOrigins.Length == 0)
        {
            if (environment.IsDevelopment())
            {
                // In Dev, we log a loud warning but don't crash.
                // We build a temporary logger since the DI container isn't ready yet.
                using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                var logger = loggerFactory.CreateLogger("CorsConfiguration");
                
                logger.LogWarning("⚠️ CORS 'AllowedOrigins' is missing. Cross-origin requests from Angular will fail.");
                
                // Return early to avoid crashing, but the policy won't be configured.
                return services;
            }

            throw new InvalidOperationException(
                "CORS 'AllowedOrigins' is missing in appsettings.json or Environment Variables. " +
                "The application cannot start without a defined security boundary.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            });
        });

        return services;
    }
}