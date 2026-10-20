using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace OurSpace.Identity.Infrastructure;

public static class IdentityRegistration
{
    public static IServiceCollection AddIdentityFoundation(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase)
    {
        services.AddDataProtection().SetApplicationName("OurSpace.Identity");
        services.AddDbContext<IdentityStoreContext>((provider, options) => { configureDatabase(provider, options); options.UseOpenIddict(); });
        services.AddIdentityCore<IdentityUserAccount>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = true;
            options.Password.RequiredLength = 12;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddSignInManager()
        .AddEntityFrameworkStores<IdentityStoreContext>()
        .AddDefaultTokenProviders();
        return services;
    }
}
