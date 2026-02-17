using System.ComponentModel.DataAnnotations;

namespace Giretra.Web.Models.Requests;

public sealed class UpdateEloVisibilityRequest
{
    [Required]
    public required bool IsPublic { get; init; }
}
