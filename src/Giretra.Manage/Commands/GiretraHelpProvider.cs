using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Giretra.Manage.Commands;

internal sealed class GiretraHelpProvider : HelpProvider
{
    protected override int MaximumIndirectExamples => 7;

    public GiretraHelpProvider(ICommandAppSettings settings) : base(settings) { }

    public override IEnumerable<IRenderable> GetHeader(ICommandModel model, ICommandInfo? command)
    {
        // Only show the branded header on root help (no specific command)
        if (command is null)
        {
            var version = model.ApplicationVersion is { } v ? $" v{v}" : string.Empty;

            yield return new Markup($"[bold blue]Giretra Manage[/][dim]{Markup.Escape(version)}[/]");
            yield return Text.NewLine;
            yield return new Markup("[dim]Tournament runner, agent validator, and database management tools.[/]");
            yield return Text.NewLine;
            yield return Text.NewLine;
        }
    }

    public override IEnumerable<IRenderable> GetFooter(ICommandModel model, ICommandInfo? command)
    {
        yield return Text.NewLine;
        yield return new Markup("[dim]Run[/] [blue]giretra-manage <command> --help[/] [dim]for more information about a command.[/]");
        yield return Text.NewLine;
    }
}
