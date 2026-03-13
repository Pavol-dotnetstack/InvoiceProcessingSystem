using InvoiceSystem.Infrastructure.Persistence;
using InvoiceSystem.WebAPI.Extensions;
using InvoiceSystem.WebAPI.Middleware;

namespace InvoiceSystem.WebAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddCustomCors(builder.Configuration, builder.Environment);
        builder.Services.AddCustomPostgreSQL(builder.Configuration);

        // Infrastructure & Application Services
        builder.Services.AddOpenApi(); // .NET 10 Standard
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails(); // Generates standardized error metadata

        //builder.Services.AddScoped<ApplicationDbContext>(); // Register DbContext for DI
            //.AddScoped<InvoiceRepository>()
            //.AddScoped<CustomerRepository>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseCors(CorsServiceExtensions.PolicyName);

        // HTTP Pipeline
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();

        // Standard Health Check
        app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Version = "10.0" }));

        app.Run();
    }
}