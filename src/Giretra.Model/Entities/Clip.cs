using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

/// <summary>
/// A shareable clip: a trick range of one deal of a finished match, rendered to a poster
/// and a video by the render worker. The files are a cache; everything needed to render
/// them again is in the deal actions.
/// </summary>
[Index(nameof(ShortId), IsUnique = true)]
[Index(nameof(MatchId), nameof(DealNumber), nameof(FromTrick), nameof(ToTrick), nameof(ViewPosition), nameof(ShowAllHands), nameof(Locale), IsUnique = true)]
[Index(nameof(Status), nameof(CreatedAt))]
[Index(nameof(CreatedByPlayerId))]
public class Clip
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Public identifier used in URLs (base62, 10 chars).</summary>
    [MaxLength(16)]
    public string ShortId { get; set; } = null!;

    public Guid MatchId { get; set; }

    public short DealNumber { get; set; }

    public short FromTrick { get; set; }

    public short ToTrick { get; set; }

    /// <summary>Seat drawn at the bottom; its hand is face up.</summary>
    public PlayerPosition ViewPosition { get; set; }

    public bool ShowAllHands { get; set; }

    [MaxLength(8)]
    public string Locale { get; set; } = "en";

    public ClipStatus Status { get; set; }

    public Guid CreatedByPlayerId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When a render worker claimed the job; stale claims are re-queued.</summary>
    public DateTimeOffset? ClaimedAt { get; set; }

    public DateTimeOffset? RenderedAt { get; set; }

    public DateTimeOffset? LastViewedAt { get; set; }

    public int ViewCount { get; set; }

    public int Attempts { get; set; }

    /// <summary>Length of the rendered video in milliseconds (null until rendered).</summary>
    public int? DurationMs { get; set; }

    public bool HasPoster { get; set; }

    public bool HasVideo { get; set; }

    [MaxLength(1024)]
    public string? Error { get; set; }

    [ForeignKey(nameof(MatchId))]
    public Match Match { get; set; } = null!;

    [ForeignKey(nameof(CreatedByPlayerId))]
    public Player CreatedBy { get; set; } = null!;
}
