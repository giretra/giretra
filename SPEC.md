# Giretra - Complete Game Specification

## 1. Overview

Giretra is a trick-taking card game from Madagascar, derived from Belote. It is played by 4 players in 2 teams of 2, with teammates sitting across from each other.

### 1.1 Objective

Be the first team to accumulate **150 match points** across multiple deals.

### 1.2 Players and Seating

- 4 players divided into 2 teams
- Teammates sit opposite each other
- Play proceeds clockwise
- Player positions (for reference): Bottom, Left, Top, Right
  - Team 1: Bottom and Top
  - Team 2: Left and Right

---

## 2. Vocabulary

| Term | Definition |
|------|------------|
| **Match** | Complete game session; first team to 150 match points wins |
| **Deal** | One complete round: Cut → Distribution → Negotiation → Hand |
| **Hand** | The 8 tricks played after negotiation |
| **Trick** | One round where each player plays one card |
| **Cut** | Splitting the deck before dealing (minimum 6 cards from top or bottom) |
| **Trump** | The privileged suit in Colour mode |
| **Lead** | The first card played in a trick |
| **Follow** | Playing a card matching the lead suit |
| **Ruff** | Playing a trump card when unable to follow suit |
| **Discard** | Playing a non-trump card when unable to follow suit |
| **Overtrump** | Playing a higher trump than one already played in the trick |
| **Accept** | Agreeing to the current bid (from French "Bonne") |
| **Double** | Challenging the opponent's bid for double stakes (from French "Contré") |
| **Redouble** | Responding to a Double with quadruple stakes (from French "Surcontré") |
| **Last Trick Bonus** | 10 bonus points for winning the final trick (from French "Dix de Der") |
| **Sweep** | Winning all 8 tricks (from French "Capot") |
| **Announcer Team** | The team whose bid determines the game mode |
| **Defender Team** | The opposing team |
| **Card Points** | Points earned from captured cards during a hand |
| **Match Points** | Points accumulated toward the 150-point victory condition |

---

## 3. Equipment

### 3.1 The Deck

32 cards consisting of 4 suits (Spades, Hearts, Diamonds, Clubs), each containing: A, K, Q, J, 10, 9, 8, 7

### 3.2 Deck Handling

- The deck is **never shuffled** during a match
- Before each deal, the player to the dealer's left must **cut** the deck
- Cut rules: Split the deck into two portions, place the bottom portion on top
- Minimum cut: 6 cards from either end
- Maximum cut: 26 cards
- After each hand, the winner of the last trick collects all cards

---

## 4. Game Modes

There are three game mode categories, listed from lowest to highest:

### 4.1 Colour Mode (4 variants)

One suit is designated as trump. Listed lowest to highest:

1. **Colour Clubs** (lowest)
2. **Colour Diamonds**
3. **Colour Hearts**
4. **Colour Spades**

**Card ranking in trump suit (strongest to weakest):** J, 9, A, 10, K, Q, 8, 7. When in trump mode player must player higher card the cards in current trick if possible.

**Card ranking in non-trump suits (strongest to weakest):** A, 10, K, Q, J, 9, 8, 7

### 4.2 NoTrumps Mode

No trump suit exists. All four suits are equal.

**Card ranking for all suits (strongest to weakest):** A, 10, K, Q, J, 9, 8, 7

### 4.3 AllTrumps Mode (highest)

No trump suit exists. All four suits use the strong ranking.

**Card ranking for all suits (strongest to weakest):** J, 9, A, 10, K, Q, 8, 7

### 4.4 Game Mode Hierarchy (lowest to highest)

1. Colour Clubs
2. Colour Diamonds
3. Colour Hearts
4. Colour Spades
5. NoTrumps
6. AllTrumps

---

## 5. Card Point Values

### 5.1 AllTrumps Values (or Trump Suit in Colour Mode)

| Card | Points |
|------|--------|
| J | 20 |
| 9 | 14 |
| A | 11 |
| 10 | 10 |
| K | 4 |
| Q | 3 |
| 8 | 0 |
| 7 | 0 |
| **Total per suit** | **62** |

### 5.2 NoTrumps Values (or Non-Trump Suits in Colour Mode)

| Card | Points |
|------|--------|
| A | 11 |
| 10 | 10 |
| K | 4 |
| Q | 3 |
| J | 2 |
| 9 | 0 |
| 8 | 0 |
| 7 | 0 |
| **Total per suit** | **30** |

### 5.3 Total Card Points by Game Mode

| Game Mode | Calculation | Total (with Last Trick Bonus) |
|-----------|-------------|-------------------------------|
| AllTrumps | 62 × 4 + 10 | **258** |
| NoTrumps | 30 × 4 + 10 | **130** |
| Colour | 62 + (30 × 3) + 10 | **162** |

---

## 6. Deal Structure

Each deal consists of four phases:

### 6.1 Phase 1: Cut

1. Player to dealer's left cuts the deck
2. Minimum 6 cards, maximum 26 cards from either end
3. Bottom portion placed on top

### 6.2 Phase 2: Initial Distribution

1. Dealer distributes cards clockwise, starting with player to their left
2. Deal 3 cards to each player, then 2 cards to each player
3. Each player now holds 5 cards
4. 12 cards remain in the deck

### 6.3 Phase 3: Negotiation

See Section 7 for detailed negotiation rules.

### 6.4 Phase 4: Final Distribution

1. After negotiation concludes, dealer distributes remaining cards
2. Deal 3 cards to each player (same order: clockwise from dealer's left)
3. Each player now holds 8 cards

### 6.5 Dealer Rotation

- First dealer is chosen randomly at match start
- Dealer rotates clockwise after each deal

---

## 7. Negotiation

### 7.1 Starting the Negotiation

1. Player to dealer's left speaks first
2. First player **must** announce a game mode (cannot pass)
3. Negotiation proceeds clockwise

### 7.2 Player Options

When it is a player's turn to speak, they may:

| Option | Description | Restrictions |
|--------|-------------|--------------|
| **Announce** | Declare a game mode | Must be higher than current bid; one Colour announcement per team per deal |
| **Accept** | Agree to current bid | Cannot Accept own team's bid in NoTrumps or Colour Clubs (auto-Double) |
| **Double** | Challenge opponent's bid | Only against opponent's bid; doubles match points |
| **Redouble** | Counter a Double | Only available in AllTrumps and Colour (except Clubs); quadruples match points |

### 7.3 Announcement Restrictions

- A player can only announce a game mode **higher** than the current bid
- Each team may only announce **one Colour mode** per deal
  - Example: If Bottom announces Colour Clubs, Top cannot later announce Colour Hearts
  - The opposing team may still announce any Colour mode
- A player who has said Accept cannot make further announcements

### 7.4 Double Mechanics

- A player may Double any opponent's bid when it is their turn
- Multiple bids can be Doubled in the same negotiation
- **Priority rule**: If multiple bids are Doubled, the **first announced game mode** that was Doubled is played
- After a Double, subsequent players may only Accept or Redouble (for announcer team)

### 7.5 Automatic Double

- **NoTrumps**: If an opponent Accepts, it counts as a Double
- **Colour Clubs**: If an opponent Accepts, it counts as a Double

### 7.6 Redouble Restrictions

Redouble is only available for:

- AllTrumps
- Colour Spades
- Colour Hearts
- Colour Diamonds

Redouble is **not** available for:

- NoTrumps (already implicitly Doubled)
- Colour Clubs (already implicitly Doubled)

### 7.7 Negotiation End Conditions

Negotiation ends when **3 consecutive Accepts** occur after any bid or Double.

### 7.8 Negotiation Examples

**Example 1: Simple negotiation**

1. Bottom: Colour Hearts
2. Left: Accept
3. Top: Accept
4. Right: Accept

→ Colour Hearts is played, Bottom's team announces

**Example 2: Outbidding**

1. Bottom: Colour Clubs
2. Left: Colour Spades
3. Top: Accept
4. Right: Accept
5. Bottom: Accept

→ Colour Spades is played, Left's team announces

**Example 3: Double with priority**

1. Bottom: Colour Clubs
2. Left: Colour Hearts
3. Top: Double (on Colour Hearts)
4. Right: Double (on Colour Clubs)

→ Colour Clubs Doubled is played (first announced mode that was Doubled)

**Example 4: Redouble**

1. Bottom: Colour Spades
2. Left: Double
3. Top: Accept
4. Right: Accept
5. Bottom: Redouble
6. Left: Accept
7. Top: Accept
8. Right: Accept

→ Colour Spades Redoubled is played

---

## 8. Playing a Hand

### 8.1 Starting the Hand

- The player to the dealer's left leads the first trick
- Any card may be led

### 8.2 Following Suit

When a card is led, subsequent players must follow these rules in order:

**Rule 1: Follow suit if possible**

If you have a card of the led suit, you must play it.

**Rule 2: In AllTrumps and NoTrumps - play higher if possible**

When following suit, you must play a higher card than the current highest if you can.

**Rule 3: In Colour mode - trump if you cannot follow**

If you cannot follow the led suit and it is not trump:

- You **must** play trump if you have trump
- **Exception**: If your teammate is currently winning with a non-trump card, you may discard instead

**Rule 4: Overtrump if trump is led or played**

If trump has been played in the trick (whether led or ruffed):

- You must play a higher trump if you have one
- This applies even if your teammate played the trump

**Rule 5: Discard if no other option**

If you cannot follow suit and have no trump (or in NoTrumps/AllTrumps, cannot beat current card):

- You may play any card

### 8.3 Playing Rules Summary Table

| Situation | Your Hand | Teammate Winning? | Trump Played? | Action Required |
|-----------|-----------|-------------------|---------------|-----------------|
| Can follow suit | Has led suit | - | - | Must follow suit |
| Can follow + beat | Has higher card of led suit | - | - | Must play higher (AllTrumps/NoTrumps) |
| Cannot follow (Colour mode) | Has trump | No | No | Must play trump |
| Cannot follow (Colour mode) | Has trump | Yes (non-trump) | No | May discard |
| Cannot follow (Colour mode) | Has trump | Yes (trump) | Yes | Must overtrump if possible |
| Cannot follow (Colour mode) | Has trump | No | Yes | Must overtrump if possible |
| Cannot follow | No trump | - | - | May discard any card |

### 8.4 Winning a Trick

- The highest card of the led suit wins, unless trump is played
- If trump is played, the highest trump wins
- The trick winner collects all 4 cards and leads the next trick

### 8.5 Last Trick Bonus

The team that wins the 8th (final) trick receives 10 bonus card points.

---

## 9. Scoring

### 9.1 Match Points by Game Mode

| Game Mode | Base Match Points | Sweep Bonus | Minimum Card Points to Win |
|-----------|-------------------|-------------|----------------------------|
| AllTrumps | 26 (split) | 35 | 129 (to avoid losing) |
| NoTrumps | 52 | 90 | 65 |
| Colour | 16 | Instant match win | 82 |

### 9.2 Multipliers

| Condition | Multiplier |
|-----------|------------|
| Normal | ×1 |
| Doubled | ×2 |
| Redoubled | ×4 |

### 9.3 AllTrumps Scoring (Split System)

AllTrumps is unique in having split scoring:

1. Total card points available: 258
2. Each team's card points are divided by 10 and rounded up
3. This gives each team's match points (max total: 26)
4. Maximum split: 20-6

**Winning conditions:**

- Announcer team needs ≥129 card points to not lose outright
- If announcer team gets <129 points: they score 0, opponents score 26
- If both teams get exactly 129 points: 0-0 (no points awarded)
- If announcer gets 131 and opponent gets 127: rounds to 13-13, treated as tie (0-0)
- If announcer gets 132 and opponent gets 126: scores 14-12

**Examples:**

| Announcer Points | Opponent Points | Announcer Match Pts | Opponent Match Pts |
|------------------|-----------------|---------------------|-------------------|
| 199 | 59 | 20 | 6 |
| 150 | 108 | 15 | 11 |
| 131 | 127 | 0 | 0 |
| 129 | 129 | 0 | 0 |
| 120 | 138 | 0 | 26 |

### 9.4 NoTrumps Scoring (Winner Takes All)

- Total card points available: 130
- Announcer needs ≥65 card points to win
- Winner takes all 52 match points
- Tie (65-65): 0-0

### 9.5 Colour Scoring (Winner Takes All)

- Total card points available: 162
- Announcer needs ≥82 card points to win
- Winner takes all 16 match points
- Tie (81-81): 0-0

### 9.6 Sweep Bonus

If a team wins all 8 tricks:

| Game Mode | Sweep Reward |
|-----------|--------------|
| AllTrumps | 35 match points (replaces normal scoring) |
| NoTrumps | 90 match points (replaces normal scoring) |
| Colour | **Instant match victory** (regardless of current score) |

Sweep bonuses are also affected by Double/Redouble multipliers.

### 9.7 Announcer Team Loses

When the announcer team fails to reach the minimum card points:

- Announcer team receives **0 match points**
- Opponent team receives **full match points** (with multipliers if applicable)

---

## 10. Winning the Match

### 10.1 Victory Condition

First team to reach **150 match points** wins the match.

### 10.2 Both Teams Exceed Target

If both teams reach or exceed 150 points in the same deal:

- The match continues
- New target becomes **250 points**
- If both exceed 250, target becomes **350**, and so on

### 10.3 Exact Tie at Target

If one team has exactly 150 and the other has more, play continues to 250.

---

## 11. Complete Deal Example

**Setup:**

- Dealer: Right
- Cut by: Bottom
- First to speak: Bottom
- Teams: Bottom+Top vs Left+Right

**Negotiation (5 cards each):**

1. Bottom: Colour Diamonds
2. Left: Colour Spades
3. Top: Accept
4. Right: Accept
5. Bottom: Accept

→ Game mode: Colour Spades, Announcer team: Left+Right

**Final distribution:** 3 more cards dealt to each player

**Hand play:** 8 tricks played following the rules

**Scoring example:**

- Left+Right (announcers) collect 95 card points
- Bottom+Top (defenders) collect 67 card points
- Announcers have ≥82, so they win
- Left+Right receive 16 match points

---

## 12. Quick Reference Card

### Card Rankings

**AllTrumps / Trump suit:** J > 9 > A > 10 > K > Q > 8 > 7

**NoTrumps / Non-trump suits:** A > 10 > K > Q > J > 9 > 8 > 7

### Game Mode Hierarchy (low to high)

Clubs < Diamonds < Hearts < Spades < NoTrumps < AllTrumps

### Key Thresholds

| Mode | Total Points | To Win | Match Points |
|------|--------------|--------|--------------|
| AllTrumps | 258 | 129+ | 26 (split) |
| NoTrumps | 130 | 65+ | 52 |
| Colour | 162 | 82+ | 16 |

### Multipliers

- Doubled: ×2
- Redoubled: ×4

### Match Victory

- First to 150 match points
- Colour Sweep = instant win