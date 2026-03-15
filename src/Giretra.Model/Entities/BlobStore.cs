using System.ComponentModel.DataAnnotations;

namespace Giretra.Model.Entities;

public class BlobStore
{
    [Key, MaxLength(256)]
    public string Key { get; set; } = null!;

    [Required]
    public byte[] Data { get; set; } = null!;

    [Required, MaxLength(64)]
    public string ContentType { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
