using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

[Table("DC_Eqps")]
public class DcEqp
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty;

    [Required]
    [StringLength(1)]
    public string Line { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Note { get; set; }
}
