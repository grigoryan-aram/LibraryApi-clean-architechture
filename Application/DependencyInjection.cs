using Application.Behaviors;
using Application.Jobs;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;




namespace Application.DependencyInjection;

public static class DependencyInjection
{

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));


        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);


        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehavior<,>));


        // Hangfire resolves job classes from the DI container, so the job type
        // itself has to be registered (its IEmailService dependency comes from
        // Infrastructure).
        services.AddScoped<SendWelcomeEmailJob>();


        return services;
    }


}


