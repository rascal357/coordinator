using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

[Table("DC_Wips")]
public class DcWip
{
    [Key]
    public int Id { get; set; }

    public int Priority { get; set; }

    [Required]
    [StringLength(50)]
    public string Technology { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Carrier { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LotId { get; set; } = string.Empty;

    public int Qty { get; set; }

    [StringLength(100)]
    public string PartName { get; set; } = string.Empty;

    [StringLength(50)]
    public string CurrentStage { get; set; } = string.Empty;

    [StringLength(50)]
    public string CurrentStep { get; set; } = string.Empty;

    [StringLength(50)]
    public string TargetStage { get; set; } = string.Empty;

    [StringLength(50)]
    public string TargetStep { get; set; } = string.Empty;

    [StringLength(50)]
    public string TargetEqpId { get; set; } = string.Empty;

    [StringLength(50)]
    public string TargetPPID { get; set; } = string.Empty;

    [StringLength(50)]
    public string State { get; set; } = string.Empty;

    [StringLength(50)]
    public string Next1 { get; set; } = string.Empty;

    [StringLength(50)]
    public string Next2 { get; set; } = string.Empty;

    [StringLength(50)]
    public string Next3 { get; set; } = string.Empty;
}
