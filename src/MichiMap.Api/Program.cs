using Azure.Storage.Blobs;
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

// Blob storage: use real Azure client when connection string is configured, no-op otherwise
var blobConn = builder.Configuration.GetConnectionString("AzureBlobStorage");
if (!string.IsNullOrEmpty(blobConn))
{
    builder.Services.AddSingleton(new BlobServiceClient(blobConn));
    builder.Services.AddScoped<IBlobStorageService, AzureBlobStorageService>();
}
else
{
    builder.Services.AddScoped<IBlobStorageService, NoOpBlobStorageService>();
}

// Per-IP rate limiting on the morel submission endpoint st 5 submissions/hour
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

builder.Services.AddCors(options =>
    options.AddPolicy("AllowFrontend", policy =>
        policy.WithOrigins(builder.Configuration["AllowedOrigins"] ?? "*")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MichiMapDbContext>();
    db.Database.Migrate();
    await DbInitializer.SeedAsync(db);
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.MapControllers();

app.Run();
