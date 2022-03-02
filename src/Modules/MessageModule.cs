﻿using Discord;
using Discord.Interactions;
using Fergun.Extensions;
using GTranslate;
using GTranslate.Results;
using GTranslate.Translators;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

public class MessageModule : InteractionModuleBase<ShardedInteractionContext>
{
    private readonly ILogger<MessageModule> _logger;
    private readonly AggregateTranslator _translator;
    private readonly GoogleTranslator _googleTranslator;
    private readonly GoogleTranslator2 _googleTranslator2;
    private readonly BingTranslator _bingTranslator;
    private readonly MicrosoftTranslator _microsoftTranslator;
    private readonly YandexTranslator _yandexTranslator;
    private static readonly Lazy<Language[]> _lazyFilteredLanguages = new(() => Language.LanguageDictionary
        .Values
        .Where(x => x.SupportedServices == (TranslationServices.Google | TranslationServices.Bing | TranslationServices.Yandex | TranslationServices.Microsoft))
        .ToArray());

    public MessageModule(ILogger<MessageModule> logger, AggregateTranslator translator, GoogleTranslator googleTranslator,
        GoogleTranslator2 googleTranslator2, BingTranslator bingTranslator, MicrosoftTranslator microsoftTranslator, YandexTranslator yandexTranslator)
    {
        _logger = logger;
        _translator = translator;
        _googleTranslator = googleTranslator;
        _googleTranslator2 = googleTranslator2;
        _bingTranslator = bingTranslator;
        _microsoftTranslator = microsoftTranslator;
        _yandexTranslator = yandexTranslator;
    }

    [MessageCommand("Get Reference")]
    public async Task GetReferencedMessage(IUserMessage message)
    {
        if (message.Type != MessageType.Reply)
        {
            await Context.Interaction.RespondWarningAsync("Message is not an inline reply.", true);
            return;
        }

        if (message.Reference?.MessageId.IsSpecified is not true)
        {
            await Context.Interaction.RespondWarningAsync("Unable to get the referenced message.", true);
            return;
        }

        string url = $"https://discord.com/channels/{message.Reference.GuildId.ToNullable()?.ToString() ?? "@me"}/{message.Reference.ChannelId}/{message.Reference.MessageId}";

        var button = new ComponentBuilder()
            .WithButton("Jump to message", style: ButtonStyle.Link, url: url)
            .Build();

        await RespondAsync("\u200b", ephemeral: true, components: button);
    }
}