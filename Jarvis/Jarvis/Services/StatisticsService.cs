namespace Jarvis.Services;

/// <summary>
/// Singleton service, manages statistics of song listens
/// </summary>
public class StatisticsService(
    IServiceScopeFactory scopeFactory,
    ILogger<StatisticsService> logger)
{
}
