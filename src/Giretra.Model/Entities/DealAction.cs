using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Giretra.Model.Enums;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model.Entities;

[Index(nameof(DealId), nameof(ActionOrder), IsUnique = true)]
public class DealAction
{
    [Key]
    public Guid Id { get; set; }

    public Guid DealId { get; set; }

    public short ActionOrder { get; set; }

    public ActionType ActionType { get; set; }

    public PlayerPosition PlayerPosition { get; set; }

    public CardRank? CardRank { get; set; }

    public CardSuit? CardSuit { get; set; }

    public GameMode? GameMode { get; set; }

    public short? CutPosition { get; set; }

    public bool? CutFromTop { get; set; }

    public short? TrickNumber { get; set; }

    [ForeignKey(nameof(DealId))]
    public Deal Deal { get; set; } = null!;
}
