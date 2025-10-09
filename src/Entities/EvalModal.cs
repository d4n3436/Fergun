using Discord;
using Discord.Interactions;
using JetBrains.Annotations;

namespace Fergun;

[UsedImplicitly]
public class EvalModal : IModal
{
    public const string CodeCustomId = "evalModalCode";

    /// <inheritdoc />
    public string Title => null!; // This is set later

    [RequiredInput]
    [ModalTextInput(CodeCustomId, TextInputStyle.Paragraph, "2 + 2")]
    public string Code { get; set; } = null!;
}