using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using A23_MVVM.Models;
using LibVLCSharp.Shared;
using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace A23_MVVM.ViewModels
{
  public partial class MainViewModel : ObservableObject
  {
    private LibVLC? _libVLC;

    [ObservableProperty]
    private MediaPlayer? _mediaPlayer;

    [ObservableProperty]
    private ObservableCollection<Scene> _scenes;

    [ObservableProperty]
    private Scene? _selectedScene;

    private bool _isUpdatingSelection = false;

    public MainViewModel()
    {
      _libVLC = new LibVLC();

      Scenes = new ObservableCollection<Scene>();
      LoadDummyData();

    }

    private void OnMediaPlayerTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
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
      if (value != null && !_isUpdatingSelection && MediaPlayer != null)
      {
        _isUpdatingSelection = true;
        PlaySelectedSceneCommand.NotifyCanExecuteChanged();
        PlaySelectedSceneCommand.Execute(null);

        _ = Task.Delay(500).ContinueWith(_ =>
        {
          App.Current.Dispatcher.Invoke(() => _isUpdatingSelection = false);
        });
      }
    }
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

      SelectedScene = Scenes.FirstOrDefault(); // リストの最初の要素
    }

    private bool CanPlaySelectedScene()
    {
      return SelectedScene != null &&
             _libVLC != null &&
             _mediaPlayer  != null &&
             !string.IsNullOrEmpty(SelectedScene.SourceVideoPath) &&
             File.Exists(SelectedScene.SourceVideoPath);
    }

    [RelayCommand(CanExecute = nameof(CanPlaySelectedScene))]
    private void PlaySelectedScene()
    {
      if (SelectedScene == null || string.IsNullOrEmpty(SelectedScene.SourceVideoPath)) return;

      var newMediaUri = new Uri(SelectedScene.SourceVideoPath);
      if (MediaPlayer.Media == null || MediaPlayer.Media.Mrl != newMediaUri.AbsoluteUri)
      {
        var media = new Media(_libVLC!, newMediaUri);
        MediaPlayer.Media = media;
      }

      // 再生速度を設定
      MediaPlayer.SetRate((float)SelectedScene.PlaybackSpeed);
      MediaPlayer.TimeChanged -= OnMediaPlayerTimeChanged;
      MediaPlayer.Time = (long)SelectedScene.StartTime.TotalMilliseconds;
      MediaPlayer.Play();
      MediaPlayer.TimeChanged += OnMediaPlayerTimeChanged;
    }

    [RelayCommand]
    private void ViewIsReady()
    {
      Debug.WriteLine("View is Loaded. Starting media plyaback(MVVM)");
      _libVLC = new LibVLC();
      MediaPlayer = new MediaPlayer(_libVLC);
      MediaPlayer.TimeChanged += OnMediaPlayerTimeChanged;
      if (PlaySelectedSceneCommand.CanExecute(null))
      {
        PlaySelectedSceneCommand.Execute(null);
      }
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