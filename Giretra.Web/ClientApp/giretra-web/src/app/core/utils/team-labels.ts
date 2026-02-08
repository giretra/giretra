import { Team } from '../../api/generated/signalr-types.generated';

export function getTeamLabel(
  team: 'Team1' | 'Team2',
  myTeam: Team | null
): string {
  if (!myTeam) return team === 'Team1' ? 'Team 1' : 'Team 2';
  if (team === 'Team1') {
    return myTeam === 'Team1' ? 'Your Team' : 'Opponents';
  }
  return myTeam === 'Team2' ? 'Your Team' : 'Opponents';
}
