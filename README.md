<p align="center">
  <img src="assets/giretra-banner.png" alt="Giretra" height="100" />
</p>

<p align="center">
  A Malagasy Belote card game you can play online or in a terminal.
</p>

---

A trick-taking card game where trash talk is a core mechanic, the kind of game that makes you trade a med diploma for one more round. Derived from Belote, played 4 players in 2 teams, the rules have survived generations almost entirely through word of mouth. 

This project is an attempt to preserve those rules properly, make the game playable online, and give developers a platform to build their own bots. The engine handles all the game logic (negotiation, trump rules, scoring, ...), and there are two ways to play: a web app with real-time multiplayer, or a terminal UI for quick local games against AI.

- **[Play online](https://play.giretra.com)**
- **[Learn the rules](https://www.giretra.com/learn)**
- **[Build your own bot](https://www.giretra.com/build-your-bot)**

## What's inside

| Project | What it does |
|---------|-------------|
| **Giretra.Core** | The game engine. All the rules, card logic, scoring, negotiation. Pure C# class library, no UI dependencies. |
| **Giretra.Web** | Online multiplayer backend built on ASP.NET Core with an Angular frontend. Real-time gameplay via SignalR, authenticated with Keycloak. |
| **Giretra** (Console) | Terminal UI for playing locally. Built with Spectre.Console, arrow keys to pick cards, quick games against AI. |
| **Giretra.Model** | Database layer. Entity Framework Core with PostgreSQL for users, matches, ELO history, and more. |
| **Giretra.Manage** (`giretra-manage`) | CLI tool for bot validation, head-to-head benchmarks, Swiss tournaments, and statistical analysis. |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) (for the web frontend)

Depending on the language you use to build your bot, you may need additional dependencies:

- Python 3.7+ for Python bots
- Go 1.22+ for Go bots
- Java 17+ for Java bots

## Build

```bash
dotnet build
dotnet test
```

## Running the web app locally

The `--offline` flag disables remote backend integrations (Keycloak auth, PostgreSQL). This is useful for testing local bots without setting up any infrastructure.

Start the backend:

```bash
dotnet run --project src/Giretra.Web -- --offline
```

The Angular frontend needs to be started separately:

```bash
cd src/Giretra.Web/ClientApp/giretra-web
npm install
npm run start
```

The web app will be served at http://localhost:4200.

## Testing with giretra-manage

`giretra-manage` is a CLI tool for validating and benchmarking bots. Run it with `--help` to see available commands:

```bash
sh giretra-manage.sh --help
```

A few examples:

```bash
# Run a head-to-head benchmark between two agents
sh giretra-manage.sh benchmark MyBot CalculatingPlayer -n 500

# Run a Swiss tournament
sh giretra-manage.sh swiss BotA BotB BotC --seed 42

# Validate that a bot plays by the rules
sh giretra-manage.sh validate MyBot -d -v --timeout 200
```

## Running the console game

```bash
dotnet run --project src/Giretra
```

Arrow keys to pick cards, Enter to play. You'll be matched against AI opponents right away.

## Contributing

Contributions are welcome and genuinely appreciated. Bug reports, typo fixes, new bot strategies, design work, translations, or just playing the game and telling us what broke. Check out the **[contributing guide](CONTRIBUTING.md)** to get started.

## License

This project is licensed under [Apache 2.0](LICENSE). External bots in the `external-bots/` directory may have their own licenses.
