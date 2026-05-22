using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MichiMap.Api.Repositories;

namespace MichiMap.Functions;

// Placeholder for Michigan DNR data fetchers (controlled burns, fish stocking).
// Runs daily at 06:00 UTC.
public class DnrFetcherFunction(IEventRepository repo, ILogger<DnrFetcherFunction> logger)
{
    [Function("DnrFetcher")]
    public async Task Run([TimerTrigger("0 0 6 * * *")] TimerInfo timer)
    {
        logger.LogInformation("DNR fetcher triggered at {Time}", DateTime.UtcNow);
        // TODO Phase 3: implement Michigan DNR Open Data + Fish Stocking API calls
        await Task.CompletedTask;
    }
}
