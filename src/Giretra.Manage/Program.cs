using Giretra.Manage.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.AddCommand<BenchmarkCommand>("benchmark")
        .WithDescription("Run a head-to-head benchmark between two agents");

    config.AddCommand<SwissCommand>("swiss")
        .WithDescription("Run a Swiss tournament between multiple agents");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate that a bot agent plays by the rules and measure response times");

    config.AddCommand<SyncBotsCommand>("sync-bots")
        .WithDescription("Create or update bot entries in the database from discovered agent factories");
});

// Backward compat: if no command given, prepend "benchmark"
if (args.Length == 0 || args[0].StartsWith("-"))
    args = ["benchmark", .. args];

return await app.RunAsync(args);
