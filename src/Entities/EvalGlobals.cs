using Discord;

namespace Fergun;

/// <summary>
/// Represents the global state used in the /eval command.
/// </summary>
public sealed class EvalGlobals
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EvalGlobals"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="services">The services.</param>
    public EvalGlobals(IInteractionContext context, IServiceProvider services)
    {
        Context = context;
        Services = services;
    }

    /// <summary>
    /// Gets the context.
    /// </summary>
    public IInteractionContext Context { get; }

    /// <summary>
    /// Gets the services.
    /// </summary>
    public IServiceProvider Services { get; }
}