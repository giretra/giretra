/**
 * Pure, DOM-free timeline of a deal replay. Everything on screen derives from `frameAt(t)`,
 * so an interactive player (requestAnimationFrame) and a headless renderer (one seek per
 * frame) show exactly the same thing at the same `t`.
 *
 * No randomness, no Date.now(), no Angular.
 */
import { CardResponse, PlayerPosition, Team } from '../../api/generated/signalr-types.generated';
import {
  DealReplayResponse,
  DealReplayTrickResponse,
  TrickResponse,
} from '../../core/services/api.service';
import { getTeam } from '../../core/utils/position-utils';

export interface ReplayTiming {
  /** Title card before the first trick. */
  intro: number;
  /** Interval between two cards of a trick. */
  card: number;
  /** Duration of a card's entry (opacity/scale ramp). */
  entry: number;
  /** Time the complete trick stays on the table with the winner highlighted. */
  hold: number;
  /** Empty table between a trick and the next (cards collected). */
  collect: number;
  /** End card after the last trick. */
  outro: number;
}

export const DEFAULT_REPLAY_TIMING: ReplayTiming = {
  intro: 1500,
  card: 700,
  entry: 250,
  hold: 1200,
  collect: 400,
  outro: 2000,
};

/** 1-based inclusive trick range. */
export interface ReplayRange {
  fromTrick: number;
  toTrick: number;
}

/** Sound asset name (public/assets/sounds/{sound}.mp3) to play at `t` ms. */
export interface ReplaySoundEvent {
  t: number;
  sound: 'card_played' | 'card_played_master' | 'card_folding_started';
}

export type ReplayCaption =
  | { kind: 'intro' }
  | { kind: 'trick'; trickNumber: number }
  | { kind: 'trickWon'; trickNumber: number; winner: PlayerPosition; points: number }
  | { kind: 'outro' };

export interface ReplayFrame {
  phase: 'intro' | 'play' | 'outro';
  /** Trick being played or collected, null in intro/outro. */
  trickNumber: number | null;
  /** Cards currently on the table, null while the table is empty. */
  trick: TrickResponse | null;
  /** Entry progress (0..1) of each card on the table. */
  entryProgress: Partial<Record<PlayerPosition, number>>;
  /** Player about to play, null when the trick is complete or the table is empty. */
  activePlayer: PlayerPosition | null;
  cardCounts: Record<PlayerPosition, number>;
  tricksWon: Record<PlayerPosition, number>;
  /** Cards still in hand, per player. */
  hands: Record<PlayerPosition, CardResponse[]>;
  /** Cumulative card points, updated when a trick is collected. */
  team1CardPoints: number;
  team2CardPoints: number;
  caption: ReplayCaption;
}

const ALL_POSITIONS: PlayerPosition[] = [
  PlayerPosition.Bottom,
  PlayerPosition.Left,
  PlayerPosition.Top,
  PlayerPosition.Right,
];

function clamp01(x: number): number {
  return x < 0 ? 0 : x > 1 ? 1 : x;
}

function sameCard(a: CardResponse, b: CardResponse): boolean {
  return a.rank === b.rank && a.suit === b.suit;
}

/** Mutable accumulator used while walking the tricks; copied into frames. */
interface Progress {
  cardCounts: Record<PlayerPosition, number>;
  tricksWon: Record<PlayerPosition, number>;
  hands: Record<PlayerPosition, CardResponse[]>;
  team1CardPoints: number;
  team2CardPoints: number;
}

export class ReplayTimeline {
  readonly range: ReplayRange;
  readonly timing: ReplayTiming;
  readonly tricks: DealReplayTrickResponse[];
  /** Total length in ms, including intro and outro. */
  readonly duration: number;

  private readonly trickDuration: number;
  private readonly startingHands: Record<PlayerPosition, CardResponse[]>;

  constructor(
    readonly replay: DealReplayResponse,
    range?: Partial<ReplayRange>,
    timing: ReplayTiming = DEFAULT_REPLAY_TIMING,
  ) {
    const total = replay.tricks.length;
    const from = Math.max(1, Math.min(total, range?.fromTrick ?? 1));
    const to = Math.max(from, Math.min(total, range?.toTrick ?? total));
    this.range = { fromTrick: from, toTrick: to };
    this.timing = timing;
    this.tricks = [...replay.tricks].sort((a, b) => a.trickNumber - b.trickNumber);
    this.trickDuration = 4 * timing.card + timing.hold + timing.collect;
    this.duration = timing.intro + (to - from + 1) * this.trickDuration + timing.outro;

    this.startingHands = {} as Record<PlayerPosition, CardResponse[]>;
    for (const pos of ALL_POSITIONS) {
      this.startingHands[pos] = replay.hands.find((h) => h.position === pos)?.cards ?? [];
    }
  }

  /** Time at which the given trick (1-based) starts; clamped to the range. */
  trickStart(trickNumber: number): number {
    const n = Math.max(this.range.fromTrick, Math.min(this.range.toTrick, trickNumber));
    return this.timing.intro + (n - this.range.fromTrick) * this.trickDuration;
  }

  /**
   * Soundtrack of the range: the live table's card sound for each play (the winning card of
   * the trick gets the "master" sound) and the collect sound when the trick leaves the table.
   * Pure function of the timeline; the clip renderer mixes these into the video.
   */
  soundEvents(): ReplaySoundEvent[] {
    const { card, hold } = this.timing;
    const events: ReplaySoundEvent[] = [];
    for (let n = this.range.fromTrick; n <= this.range.toTrick; n++) {
      const trick = this.tricks[n - 1];
      const start = this.trickStart(n);
      trick.plays.forEach((play, i) => {
        events.push({
          t: start + i * card,
          sound: play.player === trick.winner ? 'card_played_master' : 'card_played',
        });
      });
      events.push({ t: start + 4 * card + hold, sound: 'card_folding_started' });
    }
    return events;
  }

  /** Points collected by the winner of a trick (difference of cumulative points). */
  trickPoints(index: number): number {
    const trick = this.tricks[index];
    const prev = index > 0 ? this.tricks[index - 1] : null;
    const winnerTeam = getTeam(trick.winner);
    const cum =
      winnerTeam === Team.Team1 ? trick.team1CumulativeCardPoints : trick.team2CumulativeCardPoints;
    const before = prev
      ? winnerTeam === Team.Team1
        ? prev.team1CumulativeCardPoints
        : prev.team2CumulativeCardPoints
      : 0;
    return cum - before;
  }

  frameAt(t: number): ReplayFrame {
    const { intro, card, entry, hold } = this.timing;
    const time = Math.max(0, Math.min(this.duration, t));

    // Everything before the first trick of the range has already happened.
    const progress = this.initialProgress();
    for (let i = 0; i < this.range.fromTrick - 1; i++) {
      this.applyTrick(progress, i, 4);
      this.collectTrick(progress, i);
    }

    if (time < intro) {
      return this.frame('intro', null, null, {}, null, progress, { kind: 'intro' });
    }

    const u = time - intro;
    const k = Math.floor(u / this.trickDuration);
    const lastIndex = this.range.toTrick - 1;
    const index = this.range.fromTrick - 1 + k;

    if (index > lastIndex) {
      for (let i = this.range.fromTrick - 1; i <= lastIndex; i++) {
        this.applyTrick(progress, i, 4);
        this.collectTrick(progress, i);
      }
      return this.frame('outro', null, null, {}, null, progress, { kind: 'outro' });
    }

    for (let i = this.range.fromTrick - 1; i < index; i++) {
      this.applyTrick(progress, i, 4);
      this.collectTrick(progress, i);
    }

    const trick = this.tricks[index];
    const w = u - k * this.trickDuration;
    const shown = Math.min(4, Math.floor(w / card) + 1);
    const collecting = w >= 4 * card + hold;

    this.applyTrick(progress, index, shown);

    if (collecting) {
      this.collectTrick(progress, index);
      return this.frame('play', trick.trickNumber, null, {}, null, progress, {
        kind: 'trickWon',
        trickNumber: trick.trickNumber,
        winner: trick.winner,
        points: this.trickPoints(index),
      });
    }

    const entryProgress: Partial<Record<PlayerPosition, number>> = {};
    for (let i = 0; i < shown; i++) {
      entryProgress[trick.plays[i].player] = clamp01((w - i * card) / entry);
    }
    const lastSettled = w - (shown - 1) * card >= entry;
    const complete = shown === 4 && lastSettled;

    const onTable: TrickResponse = {
      leader: trick.leader,
      trickNumber: trick.trickNumber,
      playedCards: trick.plays.slice(0, shown),
      isComplete: complete,
      winner: complete ? trick.winner : null,
      winningPlayer: complete ? trick.winner : null,
    };
    const activePlayer = shown < 4 ? trick.plays[shown].player : null;
    const caption: ReplayCaption = complete
      ? {
          kind: 'trickWon',
          trickNumber: trick.trickNumber,
          winner: trick.winner,
          points: this.trickPoints(index),
        }
      : { kind: 'trick', trickNumber: trick.trickNumber };

    return this.frame(
      'play',
      trick.trickNumber,
      onTable,
      entryProgress,
      activePlayer,
      progress,
      caption,
    );
  }

  private initialProgress(): Progress {
    const cardCounts = {} as Record<PlayerPosition, number>;
    const tricksWon = {} as Record<PlayerPosition, number>;
    const hands = {} as Record<PlayerPosition, CardResponse[]>;
    for (const pos of ALL_POSITIONS) {
      cardCounts[pos] = 8;
      tricksWon[pos] = 0;
      hands[pos] = this.startingHands[pos];
    }
    return { cardCounts, tricksWon, hands, team1CardPoints: 0, team2CardPoints: 0 };
  }

  /** Removes the first `shown` cards of trick `index` from the hands. */
  private applyTrick(progress: Progress, index: number, shown: number): void {
    const trick = this.tricks[index];
    for (let i = 0; i < shown; i++) {
      const play = trick.plays[i];
      progress.cardCounts[play.player]--;
      progress.hands[play.player] = progress.hands[play.player].filter(
        (c) => !sameCard(c, play.card),
      );
    }
  }

  /** Credits the winner and the points once trick `index` leaves the table. */
  private collectTrick(progress: Progress, index: number): void {
    const trick = this.tricks[index];
    progress.tricksWon[trick.winner]++;
    progress.team1CardPoints = trick.team1CumulativeCardPoints;
    progress.team2CardPoints = trick.team2CumulativeCardPoints;
  }

  private frame(
    phase: ReplayFrame['phase'],
    trickNumber: number | null,
    trick: TrickResponse | null,
    entryProgress: Partial<Record<PlayerPosition, number>>,
    activePlayer: PlayerPosition | null,
    progress: Progress,
    caption: ReplayCaption,
  ): ReplayFrame {
    return {
      phase,
      trickNumber,
      trick,
      entryProgress,
      activePlayer,
      cardCounts: { ...progress.cardCounts },
      tricksWon: { ...progress.tricksWon },
      hands: { ...progress.hands },
      team1CardPoints: progress.team1CardPoints,
      team2CardPoints: progress.team2CardPoints,
      caption,
    };
  }
}
