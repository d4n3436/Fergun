using Discord;
using Microsoft.Extensions.Localization;

namespace Fergun;

internal record ModuleOption(IEmote Emote, LocalizedString Name, LocalizedString Description);