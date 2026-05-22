using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MichiMap.Api.Repositories;
using MichiMap.Api.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<MichiMapDbContext>(options =>
            options.UseSqlServer(context.Configuration["SqlConnectionString"]));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<GeoJsonService>();
        services.AddHttpClient();
    })
    .Build();

host.Run();
