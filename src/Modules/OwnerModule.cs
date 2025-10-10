using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Fergun.Configuration;
using Fergun.Extensions;
using Fergun.Interactive;
using Fergun.Interactive.Pagination;
using Fergun.Services;
using Fergun.Utils;
using Humanizer;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fergun.Modules;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[RequireOwner]
public class OwnerModule : InteractionModuleBase
{
    private static readonly Lazy<ScriptOptions> _lazyOptions = new(() => ScriptOptions.Default
        .AddReferences(typeof(OwnerModule).Assembly)
        .WithImports("System.Linq", "Discord", "Discord.Rest", "Discord.Interactions",
            "Fergun", "Fergun.Data", "Fergun.Modules", "Fergun.Extensions", "Microsoft.Extensions.DependencyInjection"));

    private const string EvalModalKey = "eval-modal";

    private readonly IServiceProvider _services;
    private readonly ILogger<OwnerModule> _logger;
    private readonly IFergunLocalizer<OwnerModule> _localizer;
    private readonly InteractiveService _interactive;
    private readonly FergunOptions _fergunOptions;
    private readonly FergunEmoteProvider _emotes;

    public OwnerModule(IServiceProvider services, ILogger<OwnerModule> logger, IFergunLocalizer<OwnerModule> localizer,
        InteractiveService interactive, IOptionsSnapshot<FergunOptions> fergunOptions, FergunEmoteProvider emotes)
    {
        _services = services;
        _logger = logger;
        _localizer = localizer;
        _interactive = interactive;
        _fergunOptions = fergunOptions.Value;
        _emotes = emotes;
    }

    public override void BeforeExecute(ICommandInfo command) => _localizer.CurrentCulture = CultureInfo.GetCultureInfo(Context.Interaction.GetLanguageCode());

    [SlashCommand("cmd", "Starts a process and displays the output.")]
    public async Task<RuntimeResult> CmdAsync(
        [Summary(description: "The name of the file to start.")] string fileName,
        [Summary(description: "The arguments to use.")] string? arguments = null,
        [Summary(description: "No embed.")] bool noEmbed = false)
    {
        await Context.Interaction.DeferAsync();

        _logger.LogInformation("Starting process: {FileName} with arguments: {Arguments}, no embed: {NoEmbed}", fileName, arguments ?? "(None)", noEmbed);
        string? result = await CommandUtils.StartProcessAsync(fileName, arguments);

        if (string.IsNullOrWhiteSpace(result))
        {
            var embed = new EmbedBuilder()
                .WithDescription($"ℹ️ {_localizer["NoOutput"]}")
                .WithColor(Constants.DefaultColor)
                .Build();

            await Context.Interaction.FollowupAsync(embed: embed);
        }
        else
        {
            int limit = noEmbed ? DiscordConfig.MaxMessageSize : EmbedBuilder.MaxDescriptionLength;
            string sanitized = Format.Code(result.Replace('`', '´').Truncate(limit - 12), "ansi");
            string? text = null;
            Embed? embed = null;

            if (noEmbed)
            {
                text = sanitized;
            }
            else
            {
                embed = new EmbedBuilder()
                    .WithTitle(_localizer["CommandOutput"])
                    .WithDescription(sanitized)
                    .WithColor(Constants.DefaultColor)
                    .Build();
            }

            await Context.Interaction.FollowupAsync(text, embed: embed);
        }

        return FergunResult.FromSuccess();
    }

    [SlashCommand("eval", "Evaluates C# code.")]
    public async Task<RuntimeResult> EvalAsync()
    {
        _logger.LogDebug("Sending eval modal");

        await Context.Interaction.RespondWithModalAsync<EvalModal>(EvalModalKey,
            modifyModal: b => b.WithTitle(_localizer["EvalPrompt"]).UpdateTextInput(EvalModal.CodeCustomId, t => t.Label = _localizer["Code"]));

        return FergunResult.FromSuccess();
    }

    [ModalInteraction(EvalModalKey)]
    public async Task<RuntimeResult> SubmitCodeAsync(EvalModal modal)
    {
        await Context.Interaction.DeferAsync();

        string code = modal.Code;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        object? result = null;
        CompilationErrorException? exception = null;

        _logger.LogDebug("Evaluating C# code...");
        var sw = Stopwatch.StartNew();
        try
        {
            result = await CSharpScript.EvaluateAsync(code.Trim('`'), _lazyOptions.Value, new EvalGlobals(Context, _services), null, cts.Token);
        }
        catch (CompilationErrorException e)
        {
            exception = e;
        }

        sw.Stop();

        if (exception is null)
        {
            _logger.LogDebug("Finished evaluation after {Elapsed}ms", sw.ElapsedMilliseconds);
        }
        else
        {
            _logger.LogDebug(exception, "Finished evaluation after {Elapsed}ms with exception", sw.ElapsedMilliseconds);

            var embed = new EmbedBuilder()
                .WithTitle(_localizer["EvalResults"])
                .AddField(_localizer["Input"], $"```cs\n{code.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 9)}```")
                .AddField($"⚠️ {_localizer["Output"]}", $"```cs\n{string.Join('\n', exception.Diagnostics).Truncate(EmbedFieldBuilder.MaxFieldValueLength - 9)}```")
                .WithColor(Constants.DefaultColor)
                .Build();

            await Context.Interaction.FollowupAsync(embed: embed);

            return FergunResult.FromSuccess();
        }

        var chunks = (result?.ToString() ?? string.Empty).SplitForPagination(EmbedFieldBuilder.MaxFieldValueLength - 9).ToArray();
        _logger.LogDebug("Split evaluation result into {Chunks}", "chunk".ToQuantity(chunks.Length));

        if (chunks.Length == 0)
        {
            var embed = new EmbedBuilder()
                .WithTitle(_localizer["EvalResults"])
                .AddField(_localizer["Input"], $"```cs\n{code.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 9)}```")
                .AddField(_localizer["Output"], $"({_localizer["None"]})")
                .WithFooter(_localizer["ElapsedTime", sw.ElapsedMilliseconds])
                .WithColor(Constants.DefaultColor)
                .Build();

            await Context.Interaction.FollowupAsync(embed: embed);

            return FergunResult.FromSuccess();
        }

        var paginator = new LazyPaginatorBuilder()
            .AddUser(Context.User)
            .WithPageFactory(GeneratePage)
            .WithActionOnCancellation(Constants.DefaultPaginatorActionOnCancel)
            .WithActionOnTimeout(Constants.DefaultPaginatorActionOnTimeout)
            .WithMaxPageIndex(chunks.Length - 1)
            .WithFooter(PaginatorFooter.None)
            .WithFergunEmotes(_emotes)
            .WithLocalizedPrompts(_localizer)
            .Build();

        await _interactive.SendPaginatorAsync(paginator, Context.Interaction, TimeSpan.FromMinutes(20),
            InteractionResponseType.DeferredChannelMessageWithSource, cancellationToken: CancellationToken.None);

        return FergunResult.FromSuccess();

        PageBuilder GeneratePage(int index)
        {
            var chunk = chunks[index];

            return new PageBuilder()
                .WithTitle(_localizer["EvalResults"])
                .AddField(_localizer["Input"], $"```cs\n{code.Truncate(EmbedFieldBuilder.MaxFieldValueLength - 9)}```")
                .AddField(_localizer["Output"], $"```cs\n{chunk}```")
                .WithFooter(_localizer["EvalPaginatorFooter", sw.ElapsedMilliseconds, index + 1, chunks.Length])
                .WithColor(Constants.DefaultColor);
        }
    }
}