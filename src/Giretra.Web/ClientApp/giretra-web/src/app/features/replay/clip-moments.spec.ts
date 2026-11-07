import { describe, expect, it } from 'vitest';
import { ClipMoment, PlayerPosition, Team } from '../../api/generated/signalr-types.generated';
import { pickShareMoment, teamOf } from './clip-moments';

const moment = (partial: Partial<ClipMoment>): ClipMoment => ({
  kind: 'Swing',
  dealNumber: 1,
  fromTrick: 1,
  toTrick: 1,
  team: Team.Team1,
  player: null,
  achievementCode: null,
  matchPoints: 0,
  score: 0,
  ...partial,
});

describe('pickShareMoment', () => {
  it('maps seats to teams', () => {
    expect(teamOf(PlayerPosition.Bottom)).toBe(Team.Team1);
    expect(teamOf(PlayerPosition.Top)).toBe(Team.Team1);
    expect(teamOf(PlayerPosition.Left)).toBe(Team.Team2);
    expect(teamOf(PlayerPosition.Right)).toBe(Team.Team2);
  });

  it('returns null without moments or a seat, or when nothing favours my team', () => {
    expect(pickShareMoment(null, PlayerPosition.Bottom)).toBeNull();
    expect(pickShareMoment([], PlayerPosition.Bottom)).toBeNull();
    expect(pickShareMoment([moment({ team: Team.Team2, score: 90 })], null)).toBeNull();
    expect(
      pickShareMoment([moment({ team: Team.Team2, score: 90 })], PlayerPosition.Bottom),
    ).toBeNull();
  });

  it('keeps the best moment of my team', () => {
    const picked = pickShareMoment(
      [
        moment({ team: Team.Team2, score: 120, kind: 'Sweep' }),
        moment({ team: Team.Team1, score: 60, dealNumber: 2 }),
        moment({ team: Team.Team1, score: 70, dealNumber: 3 }),
      ],
      PlayerPosition.Top,
    );
    expect(picked?.dealNumber).toBe(3);
  });

  it('prefers my own play over a slightly better team moment, but not over a much better one', () => {
    const own = moment({
      team: Team.Team1,
      score: 60,
      player: PlayerPosition.Bottom,
      kind: 'BrilliantPlay',
    });
    const partner = moment({
      team: Team.Team1,
      score: 70,
      player: PlayerPosition.Top,
      kind: 'BrilliantPlay',
    });
    const sweep = moment({ team: Team.Team1, score: 100, kind: 'Sweep' });
    expect(pickShareMoment([own, partner], PlayerPosition.Bottom)).toBe(own);
    expect(pickShareMoment([own, partner, sweep], PlayerPosition.Bottom)).toBe(sweep);
  });
});
