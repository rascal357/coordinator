using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

[Table("DC_EqpTypes")]
public class DcEqpType
{
    [Key]
    [StringLength(100)]
    public string Type { get; set; } = string.Empty;

    public int Yuu { get; set; } = 0;
}
