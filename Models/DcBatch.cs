using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

[Table("DC_Batch")]
public class DcBatch
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string BatchId { get; set; } = string.Empty;

    public int Step { get; set; }

    [Required]
    [StringLength(50)]
    public string CarrierId { get; set; } = string.Empty;

    [StringLength(50)]
    public string? LotId { get; set; }

    public int Qty { get; set; }

    [StringLength(50)]
    public string? Technology { get; set; }

    [Required]
    [StringLength(50)]
    public string EqpId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string PPID { get; set; } = string.Empty;

    [StringLength(50)]
    public string NextEqpId { get; set; } = string.Empty;

    public int IsProcessed { get; set; } = 0;

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
