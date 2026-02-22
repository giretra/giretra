<p align="center">
  <img src="assets/giretra-banner.png" alt="Giretra" height="100" />
</p>

<p align="center">
  A Malagasy Belote card game you can play in your browser or terminal.
</p>

---

Giretra is a popular Malagasy card game. With family on a Sunday afternoon, between classes at school (some people have repeated a school year because they played too much of it). It's a trick-taking game derived from Belote, played by 4 players in 2 teams, and it's been passed down across generations mostly by word of mouth.

This project is an attempt to preserve those rules properly, make the game playable online, and give developers a platform to build their own bots. The engine handles all the game logic (negotiation, trump rules, scoring, the works), and there are two ways to play: a web app with real-time multiplayer, or a terminal UI for quick local games against AI.

**[Read the full rules](RULES.md)**

## What's inside

| Project | What it does |
|---------|-------------|
| **Giretra.Core** | The game engine. All the rules, card logic, scoring, negotiation. Pure C# class library, no UI dependencies. |
| **Giretra.Web** | Web app. ASP.NET Core backend + Angular 19 frontend. Real-time multiplayer via SignalR, rooms, AI bots, Keycloak auth. |
| **Giretra** (Console) | Terminal UI for playing locally. Spectre.Console, arrow keys, quick games against AI. |
| **Giretra.Model** | Database layer. EF Core + PostgreSQL. Users, matches, ELO history, the works. |
| **Giretra.Manage** | AI benchmarking tool. Pit bot strategies against each other, ELO ratings, statistical analysis. |

## Build your own bot

Giretra supports custom AI bots that can connect to the platform and compete against other players. You can write your bot in any language you like.

**[Get started at giretra.com/build-your-bot](https://giretra.com/build-your-bot)**

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) (only needed for the web frontend)

### Build and test

```bash
dotnet build
dotnet test
```

### Run the web app

```bash
dotnet run --project src/Giretra.Web
```

This starts the ASP.NET Core backend and serves the Angular frontend. You'll also need PostgreSQL and Keycloak running for the full experience. The easiest way:

```bash
docker compose -f docker-compose.infra.yml up -d
```

### Run the console game

```bash
dotnet run --project src/Giretra
```

Arrow keys to pick cards, Enter to play. You'll be up against AI opponents right away.

### Run benchmarks

```bash
dotnet run --project src/Giretra.Manage
```

### Docker

```bash
docker compose up
```

## Contributing

Contributions are welcome and genuinely appreciated. We're actively looking for co-maintainers.

Whether it's a bug report, a typo fix, a new bot strategy, or a design idea, it all counts. Check out the **[contributing guide](CONTRIBUTING.md)** to get started.

## License

[Apache 2.0](LICENSE)
