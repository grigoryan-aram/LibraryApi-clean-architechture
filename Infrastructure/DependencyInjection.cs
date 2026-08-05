using Application.RepositoryInterfaces;
using Application.ServiceInterfaces;
using FluentEmail.MailKitSmtp;
using Infrastructure.Repositories;
using Infrastructure.Services;
using LibraryApi.Application.RepositoryInterfaces;
using LibraryApi.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace Infrastructure.DependencyInjection
{
    public static class DependencyInjection
    {

        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<LibraryDBContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<LibraryDBContext>()
            .AddDefaultTokenProviders();

            services.AddScoped<IBooksRepository, BooksRepository>();
            services.AddScoped<ICategorysRepository, CategorysRepository>();
            services.AddScoped<IMembersRepository, MembersRepository>();
            services.AddScoped<ILoansRepository, LoansRepository>();
            services.AddScoped<IIdentityService, IdentityService>();


            var emailSection = configuration.GetSection("Email");

            services
                .AddFluentEmail(emailSection["From"])
                .AddMailKitSender(new SmtpClientOptions
                {
                    Server = emailSection["Smtp:Server"],
                    Port = int.Parse(emailSection["Smtp:Port"]!),
                    UseSsl = true,
                    RequiresAuthentication = true,
                    User = emailSection["User"],
                    Password = emailSection["Password"]
                });




            return services;
        }

    }
}
