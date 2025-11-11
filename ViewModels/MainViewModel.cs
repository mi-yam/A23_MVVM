using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel; // ObservableCollection (リスト) のため
using A23_MVVM.Models;               // 1-1 で作成した Scene クラスのため
using LibVLCSharp.Shared;            // LibVLCSharp のため
using System;
using System.Linq;                   // FirstOrDefault() のため
using System.IO;                     // File.Exists() のため

// フォルダ名 "ViewModels" に合わせて、名前空間が "A23_MVVM.ViewModels" になります
namespace A23_MVVM.ViewModels
{
  public partial class MainViewModel : ObservableObject
  {
    // --- LibVLCSharpのコア ---
    private LibVLC _libVLC;

    [ObservableProperty]
    private MediaPlayer _mediaPlayer; // UIのVideoViewにバインドされます

    /// <summary>
    /// ドキュメント（シーンのリスト）
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<Scene> _scenes;

    /// <summary>
    /// 現在ドキュメントで選択されている行（シーン）
    /// </summary>
    [ObservableProperty]
    // [NotifyPropertyChangedFor(...)] を削除しました (エラーの原因だったため)
    private Scene? _selectedScene;

    public MainViewModel()
    {
      // 1. LibVLCの初期化 (重要)
      Core.Initialize();
      _libVLC = new LibVLC();
      _mediaPlayer = new MediaPlayer(_libVLC);

      // 2. ダミーデータの読み込み
      LoadDummyData();

      // 3. PropertyChanged イベントハンドラは削除しました。
      //    (後述の partial void OnSelectedSceneChanged にロジックを移動したため)
    }

    /// <summary>
    /// 【★修正点 1★】
    /// [ObservableProperty] によって SelectedScene プロパティが
    /// 変更された「後」に自動的に呼び出されるメソッドを追加しました。
    /// </summary>
    /// <param name="value">新しい値 (今回は使いませんが必須)</param>
    partial void OnSelectedSceneChanged(Scene? value)
    {
      // SelectedScene が変わったので、
      // PlaySelectedSceneCommand の CanExecute (実行可能か) を
      // 再評価するよう通知します。
      PlaySelectedSceneCommand.NotifyCanExecuteChanged();

      // コンセプト：「カーソル ＝ 再生位置」の同期
      // 選択されたシーンに割り当てられた動画クリップを再生
      PlaySelectedSceneCommand.Execute(null);
    }

    /// <summary>
    /// （ダミー）台本データをロードします
    /// </summary>
    private void LoadDummyData()
    {
      // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
      // TODO: ここをご自身のPCにある適当な動画ファイル(mp4など)の
      //       フルパスに書き換えてください。
      // ★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★★
      string dummyVideoPath = @"C:\Users\mikis\Videos\サンプル\sample-2.mp4";

      Scenes = new ObservableCollection<Scene>
            {
                new Scene
                {
                    ScriptText = "1. まず、安全カバーを取り外します。",
                    SourceVideoPath = dummyVideoPath,
                    StartTime = TimeSpan.FromSeconds(10),
                    EndTime = TimeSpan.FromSeconds(15)
                },
                new Scene
                {
                    ScriptText = "2. 次に、Aボルトを緩めます。 (▶▶早送り)",
                    SourceVideoPath = dummyVideoPath,
                    StartTime = TimeSpan.FromSeconds(20),
                    EndTime = TimeSpan.FromSeconds(22),
                    PlaybackSpeed = 2.0
                },
                new Scene
                {
                    ScriptText = "3. [注意] ボルトが脱落しないよう手で押さえてください。",
                    SourceVideoPath = dummyVideoPath,
                    StartTime = TimeSpan.FromSeconds(25),
                    EndTime = TimeSpan.FromSeconds(30)
                },
                new Scene
                {
                    ScriptText = "4. （この行には映像が割り当てられていません）",
                    SourceVideoPath = null // パスがnull
                }
            };

      // 最初の行を選択状態にする
      SelectedScene = Scenes.FirstOrDefault(); // リストの最初の要素
    }

    // OnSelectedSceneChanged() メソッドは partial void に統合されたため削除

    /// <summary>
    /// [RelayCommand]の CanExecute にバインドされます。
    /// SelectedSceneに映像が割り当てられているか(かつファイルが存在するか)を返します。
    /// </summary>
    private bool CanPlaySelectedScene()
    {
      // 選択シーンがあり、パスがNullでなく、ファイルが存在する場合のみTrue
      return SelectedScene != null &&
             !string.IsNullOrEmpty(SelectedScene.SourceVideoPath) &&
             File.Exists(SelectedScene.SourceVideoPath);
    }

    /// <summary>
    /// 選択中のシーンをプレビューで再生するコマンド
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPlaySelectedScene))]
    private void PlaySelectedScene()
    {
      // CanExecuteがfalseの場合は、このコマンドは実行されません
      if (SelectedScene == null || string.IsNullOrEmpty(SelectedScene.SourceVideoPath)) return;

      // 【★修正点 2★】
      // new Uri(...) に .Mrl は無いため、
      // .AbsoluteUri (string) を使って比較するように修正しました。
      var newMediaUri = new Uri(SelectedScene.SourceVideoPath);

      if (MediaPlayer.Media == null || MediaPlayer.Media.Mrl != newMediaUri.AbsoluteUri)
      {
        // コンストラクタに渡すのは Uri オブジェクトのまま
        var media = new Media(_libVLC, newMediaUri);
        MediaPlayer.Media = media;
      }

      // 再生速度を設定
      MediaPlayer.SetRate((float)SelectedScene.PlaybackSpeed);

      // 再生開始位置を設定 (ミリ秒)
      MediaPlayer.Time = (long)SelectedScene.StartTime.TotalMilliseconds;

      // 再生
      MediaPlayer.Play();
    }
  }
}