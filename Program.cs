using Application.DependencyInjection;
using Infrastructure.DependencyInjection;
using LibraryApi.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<LibraryDBContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));


builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<LibraryDBContext>()
    .AddDefaultTokenProviders();





builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(5);
        limiterOptions.QueueLimit = 1;
    });
});



builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);




var app = builder.Build();

app.UseRateLimiter();


app.UseMiddleware<GlobalExceptionMiddleware>();


app.UseAuthentication();
app.UseAuthorization();







app.UseSwagger();
app.UseSwaggerUI();






app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDBContext>();

    dbContext.Database.Migrate();
}


app.Run();

