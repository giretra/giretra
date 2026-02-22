# Contributing to Giretra

First off — thank you for being here. Whether you're a seasoned developer, a card game enthusiast, a designer, or someone who just found a typo, **you belong here and your contribution matters**.

## Every contribution counts

We mean it. Building a game like Giretra takes far more than writing code. Here are just some of the ways you can help:

- **Report a bug** — Ran into something weird? Open an issue. You don't need to know *why* it broke, just tell us *what* happened.
- **Suggest an idea** — Have a thought about gameplay, UI, or anything else? We want to hear it.
- **Fix a typo** — A one-character fix is a real contribution. Documentation matters.
- **Write or improve docs** — Help others understand the game rules, the codebase, or how to get started.
- **Design** — Icons, card art, layouts, color palettes, animations — we need people with an eye for visual things.
- **Translate** — Giretra is a Malagasy card game. Help us make it accessible in more languages.
- **Test** — Play the game, try to break it, and tell us what you find.
- **Review pull requests** — A fresh pair of eyes catches things the author missed.
- **Answer questions** — Help other contributors in issues. Sharing what you know is a gift.

You don't need permission to start. Pick something that interests you and jump in.

## Getting started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) (for the Angular frontend)
- Git

### Build and test

```bash
git clone https://github.com/giretra/giretra.git
cd giretra
dotnet build
dotnet test
```

For the web frontend:

```bash
cd Giretra.Web/ClientApp/giretra-web
npm install
npm start
```

## How to contribute

### Reporting bugs

Open a [GitHub Issue](https://github.com/giretra/giretra/issues/new) with:

- What you expected to happen
- What actually happened
- Steps to reproduce (if you can)
- Screenshots or logs if relevant

Don't worry about formatting it perfectly. A rough description is infinitely better than silence.

### Suggesting features or improvements

Same place — open an issue. Label it as a suggestion if you can, or just say so in the title. Sketch it out however makes sense to you: words, wireframes, napkin drawings, whatever works.

### Submitting changes

1. Fork the repository
2. Create a branch from `main` (`git checkout -b my-change`)
3. Make your changes
4. Run `dotnet build` and `dotnet test` to make sure nothing is broken
5. Commit with a clear message describing *what* and *why*
6. Push to your fork and open a Pull Request

For small fixes (typos, docs, config), feel free to skip the issue and go straight to a PR.

### Design contributions

If you're contributing visual work (icons, mockups, layouts), you can:

- Attach images directly to an issue or PR
- Describe your idea in words — we can work together to bring it to life
- Share a Figma/sketch link if that's your thing

No specific format required. We'll figure it out together.

## Code guidelines

- The solution targets **.NET 10.0**
- The frontend uses **Angular 19** with standalone components
- All game engine state is **immutable** (records, ImmutableList)
- Tests use **xUnit** (core engine) and **NSubstitute** (web layer)
- Keep changes focused — one concern per PR when possible

If you're unsure about an approach, open an issue to discuss before writing a lot of code. We'd rather help you get on the right track early.

## Code of conduct

Be kind. Be respectful. Assume good intentions. We're all here because we care about this project, and everyone deserves to feel welcome regardless of their background or experience level.

## Questions?

Open an issue with a `question` label (or just ask in the title). There are no stupid questions — if something is unclear, that's a documentation problem we should fix.

---

Thank you for contributing to Giretra. Every issue filed, every line changed, every idea shared makes this project better.
