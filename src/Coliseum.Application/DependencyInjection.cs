using Coliseum.Application.Options;
using Coliseum.Application.UseCases.Battles;
using Coliseum.Application.UseCases.Leaderboard;
using Coliseum.Application.UseCases.Players;
using Coliseum.Domain.Battles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Coliseum.Application;

/// <summary>
/// Registers the application layer. Hosts bind the option sections themselves
/// (<c>services.Configure&lt;BattleRulesOptions&gt;(config.GetSection("Battle"))</c>) so this project stays free of
/// configuration packages; adapters for the ports come from the infrastructure project.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Everything the API needs: rules, every request handler and the processing handler. Requires an <c>IAuthTokenService</c>.</summary>
    public static IServiceCollection AddColiseumApplication(this IServiceCollection services)
    {
        services.AddColiseumProcessing();

        services.TryAddScoped<CreatePlayerHandler>();
        services.TryAddScoped<GetPlayerHandler>();
        services.TryAddScoped<ListPlayersHandler>();
        services.TryAddScoped<SubmitBattleHandler>();
        services.TryAddScoped<GetBattleHandler>();
        services.TryAddScoped<GetLeaderboardHandler>();
        services.TryAddScoped<UseCases.Admin.GetAdminStatsHandler>();

        return services;
    }

    /// <summary>The worker's subset: battle rules and <see cref="ProcessBattleHandler"/>. No token service needed.</summary>
    public static IServiceCollection AddColiseumProcessing(this IServiceCollection services)
    {
        services.AddOptions<BattleRulesOptions>().ValidateOnStart();
        services.TryAddSingleton<IValidateOptions<BattleRulesOptions>, BattleRulesOptionsValidator>();
        services.TryAddSingleton(provider => provider.GetRequiredService<IOptions<BattleRulesOptions>>().Value.ToRules());
        services.TryAddScoped<ProcessBattleHandler>();

        return services;
    }

    /// <summary>The worker's scheduler: one instance per process, sized from the options.</summary>
    public static IServiceCollection AddBattleScheduler(this IServiceCollection services, int maxConcurrency)
    {
        services.TryAddSingleton(new Scheduling.BattleScheduler(maxConcurrency));
        return services;
    }
}
