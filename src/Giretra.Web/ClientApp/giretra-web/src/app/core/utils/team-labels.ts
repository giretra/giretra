import { Team } from '../../api/generated/signalr-types.generated';

export function getTeamLabel(
  team: 'Team1' | 'Team2',
  myTeam: Team | null,
  translate: (key: string) => string
): string {
  if (!myTeam) return translate(team === 'Team1' ? 'teams.team1' : 'teams.team2');
  if (team === 'Team1') {
    return myTeam === 'Team1' ? translate('teams.yourTeam') : translate('teams.opponents');
  }
  return myTeam === 'Team2' ? translate('teams.yourTeam') : translate('teams.opponents');
}
