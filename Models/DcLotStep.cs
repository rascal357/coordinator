using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

[Table("DC_LotSteps")]
public class DcLotStep
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string LotId { get; set; } = string.Empty;

    public int Qty { get; set; }

    public int Step { get; set; }

    [Required]
    [StringLength(50)]
    public string EqpId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string PPID { get; set; } = string.Empty;
}
