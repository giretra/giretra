using System.ComponentModel.DataAnnotations;

namespace Giretra.Web.Models.Requests;

public sealed class UpdateDisplayNameRequest
{
    [Required]
    public required string DisplayName { get; init; }
}
