using System.ComponentModel.DataAnnotations;

namespace MichiMap.Api.Models;

public class MorelSubmission
{
    [Required]
    public required string County { get; set; }

    [Required]
    public DateOnly ObservedDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // PhotoUrl is assigned server-side after blob upload; not accepted from client
    public string? PhotoUrl { get; set; }
}
