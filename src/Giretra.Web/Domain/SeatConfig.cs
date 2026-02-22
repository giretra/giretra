namespace Giretra.Web.Domain;

public sealed class SeatConfig
{
    public SeatAccessMode AccessMode { get; set; } = SeatAccessMode.Public;
    public string? InviteToken { get; set; }
    public HashSet<Guid> KickedUserIds { get; } = [];
}
