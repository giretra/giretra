using System.ComponentModel;
using Giretra.Manage.Data;
using Giretra.Manage.Discovery;
using Giretra.Model;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Giretra.Manage.Commands;

public sealed class SyncBotsSettings : CommandSettings
{
    [CommandOption("--connection-string")]
    [Description("PostgreSQL connection string (default: from environment)")]
    public string? ConnectionString { get; init; }
}

public sealed class SyncBotsCommand : AsyncCommand<SyncBotsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, SyncBotsSettings settings, CancellationToken cancellation)
    {
        var factories = FactoryDiscovery.DiscoverAll().Values.ToList();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Discovered Agents[/]")
            .AddColumn(new TableColumn("[bold]Agent[/]"))
            .AddColumn(new TableColumn("[bold]Display Name[/]"))
            .AddColumn(new TableColumn("[bold]Factory[/]"))
            .AddColumn(new TableColumn("[bold]Id[/]"));

        foreach (var f in factories.OrderBy(f => f.AgentName))
        {
            table.AddRow(
                f.AgentName,
                f.DisplayName,
                f.GetType().FullName!,
                f.Identifier.ToString());
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var connectionString = settings.ConnectionString ?? ConnectionStringBuilder.FromEnvironment();
        await BotRatingUpdater.SyncBotsAsync(connectionString, factories);

        AnsiConsole.MarkupLine($"[green]{factories.Count} bot(s) synced to database.[/]");
        return 0;
    }
}
