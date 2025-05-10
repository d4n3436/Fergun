using Discord;
using System.Linq;

namespace Fergun.Extensions;

public static class ComponentExtensions
{
    public static TComponent? FindComponentById<TComponent>(this ContainerComponent container, int id)
        where TComponent : class, IMessageComponent
        => container.Components
               .OfType<TComponent>()
               .FirstOrDefault(x => x.Id == id)
           ?? container.Components
               .OfType<ContainerComponent>()
               .Select(x => x.FindComponentById<TComponent>(id))
               .FirstOrDefault(x => x is not null);
}