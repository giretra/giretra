using System.ComponentModel.DataAnnotations;

namespace Giretra.Web.Models.Requests;

public sealed class BlockUserRequest
{
    [Required]
    public required string Username { get; init; }

    public string? Reason { get; init; }
}
