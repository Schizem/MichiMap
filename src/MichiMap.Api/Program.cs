using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<MichiMapDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<GeoJsonService>();
builder.Services.AddSingleton<MichiganCountyService>();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("submissions", o =>
    {
        o.PermitLimit = 5;
        o.Window = TimeSpan.FromHours(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// AllowedOrigins supports comma-separated values so both the apex domain
// and www subdomain can be allowed without separate config entries.
builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(
                  (builder.Configuration["AllowedOrigins"] ?? "*")
                  .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Run migrations and seed on every startup so fresh deployments get a schema
// and default data without a separate pipeline step.
// DbInitializer.SeedAsync is idempotent - it skips if any rows already exist.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MichiMapDbContext>();
    db.Database.Migrate();
    await DbInitializer.SeedAsync(db);
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapControllers();

app.Run();
