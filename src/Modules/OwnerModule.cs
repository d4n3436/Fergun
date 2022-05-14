using Discord;
using Discord.Interactions;
using Fergun.Utils;
using Humanizer;
using Microsoft.Extensions.Logging;

namespace Fergun.Modules;

[RequireOwner]
public class OwnerModule : InteractionModuleBase
{
    private readonly ILogger<UtilityModule> _logger;
    private readonly IFergunLocalizer<UtilityModule> _localizer;

    public OwnerModule(ILogger<UtilityModule> logger, IFergunLocalizer<UtilityModule> localizer)
    {
        _logger = logger;
        _localizer = localizer;
    }

    [SlashCommand("cmd", "Executes a command.")]
    public async Task<RuntimeResult> CmdAsync([Summary(description: "The command to execute.")] string command, [Summary(description: "No embed.")] bool noEmbed = false)
    {
        await Context.Interaction.DeferAsync();

        string? result = CommandUtils.RunCommand(command);

        if (string.IsNullOrWhiteSpace(result))
        {
            await Context.Interaction.FollowupAsync(_localizer["No output."]);
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
                    .WithTitle(_localizer["Command output"])
                    .WithDescription(sanitized)
                    .WithColor(Color.Orange)
                    .Build();
            }

            await Context.Interaction.FollowupAsync(text, embed: embed);
        }

        return FergunResult.FromSuccess();
    }
}