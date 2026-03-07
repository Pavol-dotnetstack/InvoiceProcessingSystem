using Microsoft.EntityFrameworkCore;
using InvoiceSystem.Infrastructure.Persistence;

namespace InvoiceSystem.WebAPI.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddCustomPostgreSQL(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // Database Registration (PostgreSQL)
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, 
                o => o.MigrationsAssembly("InvoiceSystem.Infrastructure")));

        return services;
    }
}