namespace Giretra.Web.Achievements;

[Flags]
public enum AchievementTrigger
{
    DealEnd = 1,
    MatchEnd = 2,
    Both = DealEnd | MatchEnd
}
