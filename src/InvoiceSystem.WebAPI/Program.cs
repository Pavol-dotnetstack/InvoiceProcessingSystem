using Microsoft.EntityFrameworkCore;
using InvoiceSystem.Infrastructure.Persistence;

namespace InvoiceSystem.WebAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 1. Connection String
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // 2. Database Registration (PostgreSQL)
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, 
                o => o.MigrationsAssembly("InvoiceSystem.Infrastructure")));

        // 3. Infrastructure & Application Services
        builder.Services.AddOpenApi(); // .NET 10 Standard
        builder.Services.AddEndpointsApiExplorer();

        var app = builder.Build();

        // 4. HTTP Pipeline
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