namespace Coordinator.Models;

/// <summary>
/// レシピ情報
/// 装置とPPIDの組み合わせに対するレシピ詳細情報
/// </summary>
public class RecipeInfo
{
    /// <summary>
    /// 装置ID
    /// </summary>
    public string EqpId { get; set; } = string.Empty;

    /// <summary>
    /// プロセスレシピID
    /// </summary>
    public string PPID { get; set; } = string.Empty;

    /// <summary>
    /// OK/NG判定
    /// </summary>
    public string OkNg { get; set; } = string.Empty;

    /// <summary>
    /// 特記事項
    /// </summary>
    public string SpecialNotes { get; set; } = string.Empty;

    /// <summary>
    /// トレンチダミー要否
    /// </summary>
    public string TrenchDummy { get; set; } = string.Empty;

    /// <summary>
    /// DMタイプ
    /// </summary>
    public string DmType { get; set; } = string.Empty;

    /// <summary>
    /// TWタイプ
    /// </summary>
    public string TwType { get; set; } = string.Empty;

    /// <summary>
    /// ポジションA
    /// </summary>
    public string PosA { get; set; } = string.Empty;

    /// <summary>
    /// ポジションB
    /// </summary>
    public string PosB { get; set; } = string.Empty;

    /// <summary>
    /// ポジションC
    /// </summary>
    public string PosC { get; set; } = string.Empty;

    /// <summary>
    /// ポジションD
    /// </summary>
    public string PosD { get; set; } = string.Empty;

    /// <summary>
    /// ポジションE
    /// </summary>
    public string PosE { get; set; } = string.Empty;

    /// <summary>
    /// ポジションF
    /// </summary>
    public string PosF { get; set; } = string.Empty;
}
