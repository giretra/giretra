using System.ComponentModel.DataAnnotations;

namespace Giretra.Web.Models.Requests;

public sealed class BanUserRequest
{
    [MaxLength(500)]
    public string? Reason { get; init; }
}
