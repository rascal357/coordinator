using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

[Table("DC_Actl")]
public class DcActl
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string EqpId { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LotId { get; set; } = string.Empty;

    [Required]
    [StringLength(10)]
    public string LotType { get; set; } = string.Empty;

    public DateTime TrackInTime { get; set; }

    [StringLength(50)]
    public string Carrier { get; set; } = string.Empty;

    public int Qty { get; set; }

    [StringLength(50)]
    public string PPID { get; set; } = string.Empty;

    [StringLength(50)]
    public string Next { get; set; } = string.Empty;

    [StringLength(50)]
    public string Location { get; set; } = string.Empty;

    public DateTime? EndTime { get; set; }
}
