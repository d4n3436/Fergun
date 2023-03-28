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
    private readonly Dictionary<ModuleInfo, Type> _types = new();
    private readonly Dictionary<string, ModuleInfo> _modules = new(); // TODO: use Options pattern
    private readonly Dictionary<string, string> _supportedLocales = new()
    {
        { "es", "es-ES" }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FergunLocalizationManager"/> class.
    /// </summary>
    /// <param name="localizerFactory">The localizer factory.</param>
    public FergunLocalizationManager(IStringLocalizerFactory localizerFactory)
    {
        _localizerFactory = localizerFactory;
    }

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

    private static bool IsMatch(ReadOnlySpan<char> resourceName, ReadOnlySpan<char> groupName, IList<string> key, ReadOnlySpan<char> identifier)
    {
        var enumerator = new SpanSplitEnumerator<char>(resourceName, '.');
        int i = 0;

        while (enumerator.MoveNext())
        {
            if (i == 0 && key.Count > 1 && groupName.SequenceEqual(key[i]))
                i++;

            if (i > key.Count - 1)
                return enumerator.IsFinished && enumerator.Current.SequenceEqual(identifier);

            if (!enumerator.Current.SequenceEqual(key[i]))
                return false;

            i++;
        }

        return false;
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
        var localizer = _localizerFactory.Create(type);

        foreach (string locale in _supportedLocales.Keys)
        {
            var cultureInfo = CultureInfo.GetCultureInfo(locale);
            Thread.CurrentThread.CurrentUICulture = cultureInfo;

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