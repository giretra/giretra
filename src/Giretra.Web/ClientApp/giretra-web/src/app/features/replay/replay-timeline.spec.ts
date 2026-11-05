import { describe, expect, it } from 'vitest';
import {
  CardRank,
  CardSuit,
  PlayerPosition,
  Team,
} from '../../api/generated/signalr-types.generated';
import { DealReplayResponse, DealReplayTrickResponse } from '../../core/services/api.service';
import { DEFAULT_REPLAY_TIMING, ReplayTimeline } from './replay-timeline';

const { Bottom, Left, Top, Right } = PlayerPosition;
const order = [Bottom, Left, Top, Right];

function card(rank: CardRank, suit: CardSuit) {
  return { rank, suit, display: `${rank}${suit}` };
}

/**
 * Synthetic deal: trick k is led by the previous winner, each player plays one card of a
 * distinct suit per trick, winner cycles Bottom, Left, Top, Right, Bottom, ...
 * Points: 10 per trick, +10 on the last trick.
 */
function buildReplay(): DealReplayResponse {
  const ranks = [
    CardRank.Seven,
    CardRank.Eight,
    CardRank.Nine,
    CardRank.Ten,
    CardRank.Jack,
    CardRank.Queen,
    CardRank.King,
    CardRank.Ace,
  ];
  const suits = [CardSuit.Clubs, CardSuit.Diamonds, CardSuit.Hearts, CardSuit.Spades];
  const hands: Record<PlayerPosition, ReturnType<typeof card>[]> = {
    Bottom: [],
    Left: [],
    Top: [],
    Right: [],
  };
  order.forEach((p, pi) => {
    ranks.forEach((r) => hands[p].push(card(r, suits[pi])));
  });

  const tricks: DealReplayTrickResponse[] = [];
  let leader = Bottom;
  let t1 = 0;
  let t2 = 0;
  for (let k = 0; k < 8; k++) {
    const li = order.indexOf(leader);
    const plays = [0, 1, 2, 3].map((i) => {
      const player = order[(li + i) % 4];
      return { player, card: hands[player][k] };
    });
    const winner = order[k % 4];
    const pts = k === 7 ? 20 : 10;
    if (winner === Bottom || winner === Top) t1 += pts;
    else t2 += pts;
    tricks.push({
      trickNumber: k + 1,
      leader,
      plays,
      winner,
      team1CumulativeCardPoints: t1,
      team2CumulativeCardPoints: t2,
    });
    leader = winner;
  }

  return {
    matchId: 'm',
    dealNumber: 2,
    totalDeals: 5,
    isRanked: true,
    dealer: Right,
    gameMode: 'AllTrumps' as never,
    multiplier: 'Normal',
    announcerTeam: Team.Team1,
    team1ScoreBefore: 4,
    team2ScoreBefore: 1,
    team1MatchPointsEarned: 2,
    team2MatchPointsEarned: 0,
    team1CardPoints: t1,
    team2CardPoints: t2,
    wasSweep: false,
    sweepingTeam: null,
    isInstantWin: false,
    players: order.map((p) => ({
      position: p,
      team: p === Bottom || p === Top ? Team.Team1 : Team.Team2,
      displayName: p,
      isBot: false,
      playerId: null,
    })),
    hands: order.map((p) => ({ position: p, cards: hands[p] })),
    tricks,
  };
}

const T = DEFAULT_REPLAY_TIMING;
const trickLen = 4 * T.card + T.hold + T.collect;

describe('ReplayTimeline', () => {
  it('computes the duration from the trick range', () => {
    const full = new ReplayTimeline(buildReplay());
    expect(full.duration).toBe(T.intro + 8 * trickLen + T.outro);

    const part = new ReplayTimeline(buildReplay(), { fromTrick: 3, toTrick: 5 });
    expect(part.duration).toBe(T.intro + 3 * trickLen + T.outro);
    expect(part.trickStart(3)).toBe(T.intro);
    expect(part.trickStart(5)).toBe(T.intro + 2 * trickLen);
  });

  it('clamps an out-of-range request', () => {
    const tl = new ReplayTimeline(buildReplay(), { fromTrick: 0, toTrick: 42 });
    expect(tl.range).toEqual({ fromTrick: 1, toTrick: 8 });
  });

  it('shows the intro with full hands and no cards on the table', () => {
    const f = new ReplayTimeline(buildReplay()).frameAt(0);
    expect(f.phase).toBe('intro');
    expect(f.trick).toBeNull();
    expect(f.cardCounts).toEqual({ Bottom: 8, Left: 8, Top: 8, Right: 8 });
    expect(f.team1CardPoints).toBe(0);
    expect(f.hands.Bottom).toHaveLength(8);
  });

  it('reveals cards one by one with a time-driven entry', () => {
    const tl = new ReplayTimeline(buildReplay());
    const start = tl.trickStart(1);

    const first = tl.frameAt(start);
    expect(first.trick?.playedCards).toHaveLength(1);
    expect(first.entryProgress[Bottom]).toBe(0);
    expect(first.activePlayer).toBe(Left);
    expect(first.cardCounts.Bottom).toBe(7);
    expect(first.hands.Bottom).toHaveLength(7);

    const mid = tl.frameAt(start + T.entry / 2);
    expect(mid.entryProgress[Bottom]).toBeCloseTo(0.5);

    const settled = tl.frameAt(start + T.card);
    expect(settled.trick?.playedCards).toHaveLength(2);
    expect(settled.entryProgress[Bottom]).toBe(1);
    expect(settled.entryProgress[Left]).toBe(0);

    const complete = tl.frameAt(start + 3 * T.card + T.entry);
    expect(complete.trick?.playedCards).toHaveLength(4);
    expect(complete.trick?.winningPlayer).toBe(Bottom);
    expect(complete.activePlayer).toBeNull();
    expect(complete.caption).toEqual({
      kind: 'trickWon',
      trickNumber: 1,
      winner: Bottom,
      points: 10,
    });
    // Points are credited only when the trick is collected.
    expect(complete.team1CardPoints).toBe(0);
    expect(complete.tricksWon.Bottom).toBe(0);
  });

  it('credits the winner while the trick is collected', () => {
    const tl = new ReplayTimeline(buildReplay());
    const collecting = tl.frameAt(tl.trickStart(1) + 4 * T.card + T.hold + 1);
    expect(collecting.trick).toBeNull();
    expect(collecting.tricksWon.Bottom).toBe(1);
    expect(collecting.team1CardPoints).toBe(10);
    expect(collecting.caption.kind).toBe('trickWon');
  });

  it('starts a partial range with earlier tricks already applied', () => {
    const tl = new ReplayTimeline(buildReplay(), { fromTrick: 3, toTrick: 4 });
    const f = tl.frameAt(0);
    expect(f.phase).toBe('intro');
    expect(f.cardCounts).toEqual({ Bottom: 6, Left: 6, Top: 6, Right: 6 });
    expect(f.tricksWon).toEqual({ Bottom: 1, Left: 1, Top: 0, Right: 0 });
    expect(f.team1CardPoints).toBe(10);
    expect(f.team2CardPoints).toBe(10);

    const t3 = tl.frameAt(tl.trickStart(3));
    expect(t3.trick?.trickNumber).toBe(3);
    expect(t3.trick?.leader).toBe(Left);
  });

  it('ends with everything played and the final score', () => {
    const tl = new ReplayTimeline(buildReplay());
    const f = tl.frameAt(tl.duration);
    expect(f.phase).toBe('outro');
    expect(f.cardCounts).toEqual({ Bottom: 0, Left: 0, Top: 0, Right: 0 });
    expect(f.tricksWon).toEqual({ Bottom: 2, Left: 2, Top: 2, Right: 2 });
    expect(f.team1CardPoints + f.team2CardPoints).toBe(90);
    expect(f.hands.Right).toHaveLength(0);
  });

  it('is a pure function of t', () => {
    const tl = new ReplayTimeline(buildReplay());
    const a = JSON.stringify(tl.frameAt(12345));
    tl.frameAt(500);
    tl.frameAt(tl.duration);
    expect(JSON.stringify(tl.frameAt(12345))).toBe(a);
  });
});
