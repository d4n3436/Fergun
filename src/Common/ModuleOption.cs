using Discord;
using Microsoft.Extensions.Localization;

namespace Fergun.Common;

internal record ModuleOption(IEmote Emote, LocalizedString Name, LocalizedString Description);