using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Discord.Interactions;
using Microsoft.Extensions.Localization;

namespace Fergun;

/// <summary>
/// Represents a localization manager that uses <see cref="IStringLocalizerFactory"/> to obtain the localized resources.
/// </summary>
public sealed class FergunLocalizationManager : ILocalizationManager
{
    private const string ModulesNamespace = "Fergun.Modules";
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly Dictionary<ModuleInfo, Type> _types = [];
    private readonly Dictionary<string, ModuleInfo> _modules = []; // TODO: use Options pattern

    private readonly Dictionary<string, string> _supportedLocales = new()
    {
        { "es", "es-ES" }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FergunLocalizationManager"/> class.
    /// </summary>
    /// <param name="localizerFactory">The localizer factory.</param>
    public FergunLocalizationManager(IStringLocalizerFactory localizerFactory) => _localizerFactory = localizerFactory;

    /// <summary>
    /// Loads and caches the modules.
    /// </summary>
    /// <param name="modules">The modules.</param>
    public void AddModules(IEnumerable<ModuleInfo> modules)
    {
        foreach (var module in modules)
        {
            var type = Type.GetType($"{ModulesNamespace}.{module.Name}");

            if (type is not null)
            {
                _types.Add(module, type);
                _modules.Add(module.IsSlashGroup ? module.SlashGroupName : module.Name, module);
            }
        }
    }

    /// <inheritdoc />
    public IDictionary<string, string> GetAllDescriptions(IList<string> key, LocalizationTarget destinationType) => GetValues(key, "description");

    /// <inheritdoc />
    public IDictionary<string, string> GetAllNames(IList<string> key, LocalizationTarget destinationType) => GetValues(key, "name");

    private static bool IsMatch(string resourceName, string? groupName, IList<string> key, string identifier)
    {
        // Localized string names have the structure "command.(parameter).(choice).identifier" where identifier is either "name" or "description"
        // Examples:
        // google.name
        // google.query.description
        // avatar.type.server.name
        string[] split = resourceName.Split('.');
        var parts = groupName is null ? split : split.Prepend(groupName);

        return key.Append(identifier).SequenceEqual(parts);
    }

    private IDictionary<string, string> GetValues(IList<string> key, string identifier)
    {
        if (!_modules.TryGetValue(key[0], out var module) || !_types.TryGetValue(module, out var type))
        {
            module = _modules.Values.FirstOrDefault(x => x.SlashCommands.Any(y => y.Name == key[0]) || x.ContextCommands.Any(y => y.Name == key[0]));
            if (module is null || !_types.TryGetValue(module, out type))
            {
                return ImmutableDictionary<string, string>.Empty;
            }
        }

        var dictionary = new Dictionary<string, string>();

        // Module-specific localizer
        var localizer = _localizerFactory.Create(type);

        foreach (string locale in _supportedLocales.Keys)
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(locale);

            foreach (var localizedString in localizer.GetAllStrings(false))
            {
                if (IsMatch(localizedString.Name, module.SlashGroupName, key, identifier))
                {
                    dictionary.Add(_supportedLocales[locale], localizer[localizedString.Value]);
                }
            }
        }

        return dictionary;
    }
}