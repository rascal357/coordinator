using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

[Table("DC_Actl")]
public class DcActl
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string? EqpId { get; set; }

    [StringLength(50)]
    public string? LotId { get; set; }

    [StringLength(10)]
    public string? LotType { get; set; }

    public DateTime? TrackInTime { get; set; }

    [StringLength(50)]
    public string? Carrier { get; set; }

    public int? Qty { get; set; }

    [StringLength(50)]
    public string? PPID { get; set; }

    [StringLength(50)]
    public string? Next { get; set; }

    [StringLength(50)]
    public string? Location { get; set; }

    [StringLength(50)]
    public string? EndTime { get; set; }
}
