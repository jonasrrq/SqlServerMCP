using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Extensions.Skills;
using ModelContextProtocol.Server;

namespace SqlServerMCP.Skills;

public static class SkillServerBuilderExtensions
{
    /// <summary>
    /// Registers skills/list, skills/get, resources/list and resources/read on top of a registry.
    /// The registry is read on every request, so external reloads are visible without a restart.
    /// </summary>
    public static IMcpServerBuilder WithSkillDistribution(
        this IMcpServerBuilder builder,
        SkillRegistry registry,
        SkillOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);

        return builder
            .WithSkills(skills =>
            {
                skills.ListSkillsHandler = (context, cancellationToken) =>
                    registry.ListSkillsAsync(context.Params?.Cursor, cancellationToken);
                skills.GetSkillHandler = (context, cancellationToken) =>
                    registry.GetSkillAsync(context.Params?.Uri ?? string.Empty, cancellationToken);
            })
            .WithListResourcesHandler((context, cancellationToken) =>
                registry.ListResourcesAsync(context.Params?.Cursor, cancellationToken))
            .WithReadResourceHandler((context, cancellationToken) =>
                registry.ReadResourceAsync(context.Params?.Uri ?? string.Empty, cancellationToken));
    }
}
