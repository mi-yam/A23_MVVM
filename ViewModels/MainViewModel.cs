using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel; // ObservableCollection
using A23_MVVM.Models;               // Scene
using LibVLCSharp.Shared;            // LibVLC
using System;
using System.Linq;                   // FirstOrDefault
using System.IO;                     // File.Exists
using System.Diagnostics;            // Debug.WriteLine (デバッグ用)
using System.Threading.Tasks;        // Task.Delay (タイマー用)
using System.Diagnostics; // ★ Debug.WriteLine のため追加

// プロジェクト名.ViewModels (例: A23_MVVM.ViewModels)
namespace A23_MVVM.ViewModels
{
  public partial class MainViewModel : ObservableObject
  {
    // --- Fields ---
    private LibVLC _libVLC;

    [ObservableProperty]
    private MediaPlayer _mediaPlayer;

    [ObservableProperty]
    private ObservableCollection<Scene> _scenes;

    [ObservableProperty]
    private Scene? _selectedScene;

    /// <summary>
    /// UIからの選択（クリック）と、再生位置の自動追従が
    /// 競合するのを防ぐためのフラグ
    /// </summary>
    private bool _isUpdatingSelection = false;

    // --- Constructor ---
    public MainViewModel()
    {
      _libVLC = new LibVLC();
      _mediaPlayer = new MediaPlayer(_libVLC);

      // 2. ダミーデータの読み込み
      LoadDummyData();

      // 3. MediaPlayer の TimeChanged イベントを購読（監視）
      //    (ステップ4の追加機能)
      _mediaPlayer.TimeChanged += OnMediaPlayerTimeChanged;
    }

    // --- Event Handlers & Callbacks ---

    /// <summary>
    /// (ステップ4) MediaPlayerの再生時間が変わるたびに呼び出される
    /// </summary>
    private void OnMediaPlayerTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
      // UIスレッドで実行するおまじない
      App.Current.Dispatcher.Invoke(() =>
      {
        // UIクリック操作中は自動追従を停止
        if (_isUpdatingSelection)
        {
          return;
        }

        long currentTime = e.Time; // 現在の再生時間 (ミリ秒)

        // 1. 再生がシーンの「終了時刻」を超えたら、一時停止する
        if (SelectedScene != null && currentTime > (long)SelectedScene.EndTime.TotalMilliseconds)
        {
          if (MediaPlayer.IsPlaying)
          {
            MediaPlayer.Pause();
          }
        }

        // 2. 現在の再生時間に一致するシーンを探す (Linq)
        var currentScene = Scenes.FirstOrDefault(scene =>
            !string.IsNullOrEmpty(scene.SourceVideoPath) && // 映像があり
            currentTime >= (long)scene.StartTime.TotalMilliseconds && // 開始 <= 時間
            currentTime < (long)scene.EndTime.TotalMilliseconds   // 時間 < 終了
        );

        // もし該当するシーンがあり、それが現在の選択行と違うなら
        if (currentScene != null && currentScene != SelectedScene)
        {
          // SelectedScene を更新する (UIのハイライトが自動で追従)
          SelectedScene = currentScene;
        }
      });
    }

    /// <summary>
    /// (ステップ4) SelectedScene プロパティが変更された「後」に呼び出される
    /// (UIクリック時、または上記の自動追従時に発生)
    /// </summary>
    partial void OnSelectedSceneChanged(Scene? value)
    {
      // 自動追従 (OnMediaPlayerTimeChanged) によって value が変更された場合、
      // _isUpdatingSelection は false のため、この中には入らない。
      // UIクリックによって value が変更された場合のみ、中に入る。
      if (value != null && !_isUpdatingSelection)
      {
        // UIクリック操作中フラグを立てる
        _isUpdatingSelection = true;

        // Play コマンドの実行可否を更新
        PlaySelectedSceneCommand.NotifyCanExecuteChanged();

        // 選択シーンを再生
        PlaySelectedSceneCommand.Execute(null);

        // 0.5秒後（再生が飛んだ後）にフラグを戻す
        _ = Task.Delay(500).ContinueWith(_ =>
        {
          // UIスレッドでフラグを戻す
          App.Current.Dispatcher.Invoke(() => _isUpdatingSelection = false);
        });
      }
    }

    // --- Methods ---

    /// <summary>
    /// (ステップ3) （ダミー）台本データをロードします
    /// (これが不足していたメソッドです)
    /// </summary>
    private void LoadDummyData()
    {
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

    /// <summary>
    /// (ステップ3) [RelayCommand]の CanExecute にバインドされます。
    /// </summary>
    private bool CanPlaySelectedScene()
    {
      // 選択シーンがあり、パスがNullでなく、ファイルが存在する場合のみTrue
      return SelectedScene != null &&
             !string.IsNullOrEmpty(SelectedScene.SourceVideoPath) &&
             File.Exists(SelectedScene.SourceVideoPath);
    }

    // --- Commands ---

    /// <summary>
    /// 選択中のシーンをプレビューで再生するコマンド
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPlaySelectedScene))]
    private void PlaySelectedScene()
    {
      if (SelectedScene == null || string.IsNullOrEmpty(SelectedScene.SourceVideoPath)) return;

      var newMediaUri = new Uri(SelectedScene.SourceVideoPath);

      // メディアが設定されていないか、違う動画ファイルが設定されていたら、
      // 新しいMediaオブジェクトを作成して設定し直す
      if (MediaPlayer.Media == null || MediaPlayer.Media.Mrl != newMediaUri.AbsoluteUri)
      {
        var media = new Media(_libVLC, newMediaUri);
        MediaPlayer.Media = media;
      }

      // 再生速度を設定
      MediaPlayer.SetRate((float)SelectedScene.PlaybackSpeed);

      // (ステップ4) 再生位置を飛ばす（TimeSet）直前に
      // イベントハンドラを一時的に解除し、競合を防ぐ
      MediaPlayer.TimeChanged -= OnMediaPlayerTimeChanged;

      // 再生開始位置を設定 (ミリ秒)
      MediaPlayer.Time = (long)SelectedScene.StartTime.TotalMilliseconds;

      // 再生
      MediaPlayer.Play();

      // (ステップ4) 再生開始後に、再度イベントハンドラを登録
      MediaPlayer.TimeChanged += OnMediaPlayerTimeChanged;
    }

    [RelayCommand]
    private void ApplySpeedStyle()
    {
      // Visual Studio の「出力」ウィンドウにメッセージが表示されます
      Debug.WriteLine("リボン: [▶▶早送り] スタイルが押されました");
    }

    [RelayCommand]
    private void ApplySafetyStyle()
    {
      Debug.WriteLine("リボン: [！安全注意] スタイルが押されました");
    }

    [RelayCommand]
    private void ApplyHeading1Style()
    {
      Debug.WriteLine("リボン: [見出し1] スタイルが押されました");
    }

  }
}