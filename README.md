# Giretra

Malagasy Belote card game engine implementation.

## Build & Test

```bash
dotnet build
dotnet test
```

## Game Overview

- 4 players, 2 teams (Team1: Bottom+Top, Team2: Left+Right)
- 32-card deck (7-A in 4 suits), never shuffled, only cut between deals
- First team to **150 match points** wins

## Game Modes (lowest to highest)

1. **Colour Clubs/Diamonds/Hearts/Spades** - One suit is trump
2. **NoTrumps** - No trump, standard ranking
3. **AllTrumps** - No trump, trump ranking for all suits

## Card Rankings

| Context | Order (high to low) |
|---------|---------------------|
| Trump / AllTrumps | J > 9 > A > 10 > K > Q > 8 > 7 |
| Non-trump / NoTrumps | A > 10 > K > Q > J > 9 > 8 > 7 |

## Card Points

| Card | Trump/AllTrumps | Non-trump/NoTrumps |
|------|--------------|------------------|
| J | 20 | 2 |
| 9 | 14 | 0 |
| A | 11 | 11 |
| 10 | 10 | 10 |
| K | 4 | 4 |
| Q | 3 | 3 |
| 8,7 | 0 | 0 |

## Deal Structure

1. **Cut** - 6-26 cards from either end
2. **Initial Deal** - 5 cards each (3+2)
3. **Negotiation** - Determine game mode
4. **Final Deal** - 3 more cards each (8 total)
5. **Play** - 8 tricks

## Negotiation Rules

- First player (dealer's left) MUST announce
- Announce only higher modes than current bid
- One Colour per team per deal
- Accept on NoTrumps/ColourClubs by opponent = auto-Double
- Redouble only for AllTrumps, Spades, Hearts, Diamonds
- Ends on 3 consecutive Accepts
- Priority: first announced mode that was Doubled wins

## Playing Rules

### All Modes
- Must follow suit if able

### AllTrumps/NoTrumps
- Must play higher card if following suit and able

### Colour Mode
- Must trump if cannot follow (exception: teammate winning with non-trump)
- Must overtrump if trump already played

## Scoring

| Mode | Total | Threshold | Match Points |
|------|-------|-----------|--------------|
| AllTrumps | 258 | 129+ | 26 (split, round to nearest) |
| NoTrumps | 130 | 65+ | 52 (winner-takes-all) |
| Colour | 162 | 82+ | 16 (winner-takes-all) |

- **Last trick bonus:** +10 card points
- **Sweep (all 8 tricks):** AllTrumps=35, NoTrumps=90, Colour=instant match win
- **Multipliers:** Double ×2, Redouble ×4
- **Tie:** 0-0 (no points awarded)

## Project Structure

```
Giretra.Core/
├── Cards/       # Card, CardRank, CardSuit, Deck
├── Players/     # Player, PlayerPosition, Team
├── GameModes/   # GameMode, GameModeCategory, MultiplierState
├── Play/        # PlayedCard, CardComparer, PlayValidator
├── Negotiation/ # NegotiationAction, NegotiationEngine
├── State/       # TrickState, NegotiationState, HandState, DealState, MatchState
└── Scoring/     # DealResult, ScoringCalculator
```
