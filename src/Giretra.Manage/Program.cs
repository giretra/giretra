using Giretra.Manage.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("giretra-manage");
    config.SetApplicationVersion(GetVersion());
    config.SetHelpProvider(new GiretraHelpProvider(config.Settings));

    config.AddCommand<BenchmarkCommand>("benchmark")
        .WithDescription("Run a head-to-head benchmark between two agents")
        .WithExample("benchmark")
        .WithExample("benchmark", "MyBot", "CalculatingPlayer", "-n", "500");

    config.AddCommand<SwissCommand>("swiss")
        .WithDescription("Run a Swiss tournament between multiple agents")
        .WithExample("swiss")
        .WithExample("swiss", "BotA", "BotB", "BotC", "--seed", "42");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate that a bot agent plays by the rules and measure response times")
        .WithExample("validate", "MyBot")
        .WithExample("validate", "MyBot", "-d", "-v", "--timeout", "200");

    config.AddCommand<SyncBotsCommand>("sync-bots")
        .WithDescription("Create or update bot entries in the database from discovered agent factories")
        .WithExample("sync-bots");
});

if (args.Length == 0)
    args = ["-h"];

return await app.RunAsync(args);

static string GetVersion()
{
    var assembly = typeof(Program).Assembly;
    var version = assembly.GetName().Version;
    var informational = assembly
        .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
        .FirstOrDefault()?.InformationalVersion;

    if (informational is null)
        return version?.ToString(3) ?? "0.0.0";

    // Shorten the build metadata hash (e.g. 1.0.0+abc123def → 1.0.0+abc123d)
    var plusIndex = informational.IndexOf('+');
    if (plusIndex >= 0 && informational.Length > plusIndex + 8)
        return informational[..(plusIndex + 8)];

    return informational;
}
