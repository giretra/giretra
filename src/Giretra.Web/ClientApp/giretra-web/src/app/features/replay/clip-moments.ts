/**
 * Picks which solver-suggested moment to offer a player at match end. The server ranks
 * moments for both teams; here we keep the ones that are good news for this player and
 * prefer the ones they made themselves.
 */
import { ClipMoment, PlayerPosition, Team } from '../../api/generated/signalr-types.generated';

export function teamOf(position: PlayerPosition): Team {
  return position === PlayerPosition.Bottom || position === PlayerPosition.Top
    ? Team.Team1
    : Team.Team2;
}

export function pickShareMoment(
  moments: readonly ClipMoment[] | null | undefined,
  me: PlayerPosition | null | undefined,
): ClipMoment | null {
  if (!moments?.length || !me) return null;
  const myTeam = teamOf(me);
  const mine = moments.filter((m) => m.team === myTeam);
  if (!mine.length) return null;
  // Own plays first (a small bonus, not an override: a team sweep still beats a modest play).
  const score = (m: ClipMoment) => m.score + (m.player === me ? 15 : 0);
  return mine.reduce((best, m) => (score(m) > score(best) ? m : best), mine[0]);
}
