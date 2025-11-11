using System;

// フォルダ名 "Models" に合わせて、名前空間が "A23_MVVM.Models" になります
namespace A23_MVVM.Models
{
  /// <summary>
  /// 動画の「1行」または「1シーン」を表すマスターデータ
  /// (C++の struct や POD に相当します)
  /// </summary>
  public class Scene
  {
    /// <summary>
    /// シーンID (識別用)
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ドキュメントエリアに表示されるテキスト（台本）
    /// </summary>
    public string ScriptText { get; set; } = string.Empty;

    // --- 映像クリップ情報 ---

    /// <summary>
    /// このシーンに割り当てられたマスター動画のファイルパス
    /// </summary>
    public string? SourceVideoPath { get; set; }

    /// <summary>
    /// 割り当てられたクリップの開始時間
    /// (C#標準の TimeSpan 構造体を使います)
    /// </summary>
    public TimeSpan StartTime { get; set; }

    /// <summary>
    /// 割り当てられたクリップの終了時間
    /// </summary>
    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// 再生速度 (1.0が標準)
    /// </summary>
    public double PlaybackSpeed { get; set; } = 1.0;
  }
}