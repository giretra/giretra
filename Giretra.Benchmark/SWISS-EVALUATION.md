# Swiss Tournament ELO Evaluation

## Goal

Produce **stable, reproducible ELO ratings** for comparing AI agents. Running the same tournament twice (even with different seeds) should yield ratings within ~20 points of each other.

## The Instability Problem

A naive ELO system with constant K-factor is unstable over long tournaments:

| K | Matches | ELO noise (std dev) |
|---|---------|---------------------|
| 24 | 50 | ±85 |
| 24 | 100 | ±120 |
| 24 | 200 | ±170 |

The formula: `noise ≈ K/2 × √N`. With constant K, more rounds **increase** noise rather than converging — the ELO performs a random walk.

Two additional factors compound this:
- **Binary scoring** (1.0/0.0): a razor-thin 1000-998 win shifts ELO the same as a 1000-100 blowout
- **Swiss pairing feedback**: one upset drops a player's rank, changing pairings, creating oscillation loops

## Solution: Two Stabilization Mechanisms

### 1. Decaying K-Factor

The K-factor decays as each participant plays more matches:

```
K_effective = K_min + (K_max - K_min) / (1 + matchesPlayed / halfLife)
```

| Default | Value | Purpose |
|---------|-------|---------|
| `K_max` | 40 | Aggressive early convergence (first ~20 matches find the ballpark) |
| `K_min` | 4 | Stable late game (each match barely moves the needle) |
| `halfLife` | 30 | At 30 matches, K is halfway between max and min |

K-factor progression:

| Matches | K_effective | Per-match noise |
|---------|-------------|-----------------|
| 0 | 40.0 | ±20 |
| 10 | 31.0 | ±15.5 |
| 30 | 22.0 | ±11 |
| 60 | 16.0 | ±8 |
| 100 | 6.8 | ±3.4 |
| 200 | 6.3 | ±3.2 |

Each player has their own K-factor based on their match count. A newcomer entering late still converges quickly while established ratings stay stable.

### 2. Margin-Based Scoring

Instead of binary win/loss, the actual score reflects how decisive the victory was:

```
margin = (winnerScore - loserScore) / targetScore
actualScore = 0.5 + 0.5 × margin
```

| Result | Actual (winner) | Actual (loser) |
|--------|-----------------|----------------|
| 1000 - 0 (blowout) | 1.0 | 0.0 |
| 1000 - 500 | 0.75 | 0.25 |
| 1000 - 800 | 0.60 | 0.40 |
| 1000 - 950 (close) | 0.525 | 0.475 |

A close win barely moves ELO — it's noisy evidence of skill difference. A blowout is a strong signal and moves ELO accordingly.

## Combined Effect

| Configuration | Noise after 100 matches |
|---------------|-------------------------|
| K=24, binary (old) | ±120 points |
| K decay only | ±25 points |
| K decay + margin scoring | ±15-20 points |

## CLI Options

```bash
dotnet run --project Giretra.Benchmark -- swiss \
  -r 200          # 200 rounds
  -k 40           # K-factor max (default: 40)
  --k-min 4       # K-factor min (default: 4)
  --k-half-life 30  # Half-life in matches (default: 30)
```

## Tuning Guidelines

### Round Count
- **50-100 rounds**: Good enough for rough ranking with 3-5 agents
- **200+ rounds**: Recommended for accurate absolute ELO differences
- Diminishing returns beyond 500 rounds (K is already near minimum)

### K-Factor Tuning
- **Increase `K_max`** (e.g. 60) if agents are widely different in skill — helps reach ballpark faster
- **Decrease `K_min`** (e.g. 2) for maximum late-game stability at the cost of slower correction after upsets
- **Increase `halfLife`** (e.g. 50) if you want more exploration before ratings settle

### Target Score
- Higher target (1000+) means longer matches with less variance per match
- Lower target (150) means shorter, noisier matches — compensate with more rounds

## Interpreting Results

The final ranking table shows:
- **ELO**: The converged rating — compare differences between agents
- **ELO Range** (min-max): Smaller range = more stable = more reliable rating
- **Win%**: Raw win rate — cross-reference with ELO for consistency

A 100-point ELO difference corresponds to ~64% expected win rate. A 200-point difference corresponds to ~76%.
