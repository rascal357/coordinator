using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Coordinator.Models;

// Oracle移行時の注意点:
// 1. [Table]属性でテーブル名を明示しているため、Oracleでもこの名前でテーブルが作成される
// 2. Oracleではテーブル名・カラム名は大文字で保存されるため、
//    Entity Frameworkの規約により自動的に大文字に変換される
// 3. StringLength属性で指定した長さはOracleでは NVARCHAR2(n) にマッピングされる
// 4. DateTime型は Oracle の TIMESTAMP にマッピングされる
// 5. bool型は NUMBER(1) にマッピングされる（0=false, 1=true）
// 6. 主キー（Id）は自動採番するためにシーケンスとトリガーが必要になる場合がある
//    または、[DatabaseGenerated(DatabaseGeneratedOption.Identity)] を使用
[Table("DC_Eqps")]
public class DcEqp
{
    [Key]
    // Oracle用: 自動採番の場合は以下を追加
    // [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
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
