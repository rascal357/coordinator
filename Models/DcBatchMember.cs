using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

[Table("DC_BatchMembers")]
public class DcBatchMember
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string BatchId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string CarrierId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LotId { get; set; } = string.Empty;

    public int Qty { get; set; }

    [Required]
    [StringLength(50)]
    public string Technology { get; set; } = string.Empty;
}
