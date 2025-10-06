// in MainWindow.xaml.cs

// 1. 必要なusingディレクティブを追加
using LibVLCSharp.Shared; // VLCライブラリのコア
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using static A23_MVVM.MainWindowViewModel;

namespace A23_MVVM
{
  public partial class MainWindow : Window, IDisposable
  {
    private LibVLC _libVLC;
    private MediaPlayer _mediaPlayer;
    private DispatcherTimer _playbackTimer;
    private ClipViewModel? _currentClipInPlayer = null;

    private TimeSpan? _pendingSeekPosition = null;
    private bool _resumePlaybackAfterSeek = false;

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow()
    {
      InitializeComponent();
      var viewModel = new MainWindowViewModel();
      DataContext = viewModel;

      // --- VLCの初期化 ---
      Core.Initialize(); // ライブラリの初期化を最初に行う
      _libVLC = new LibVLC();
      _mediaPlayer = new MediaPlayer(_libVLC);
      PreviewPlayer.MediaPlayer = _mediaPlayer; // XAMLのVideoViewにMediaPlayerを接続

      // --- イベントの購読 ---
      viewModel.PlaybackActionRequested += HandlePlaybackAction;
      viewModel.SeekRequested += HandleSeekRequest; // メソッド名を変更
      _mediaPlayer.EncounteredError += (s, e) => MessageBox.Show("再生エラーが発生しました。");
      _mediaPlayer.TimeChanged += (s, e) =>
      {
        // BeginInvokeに変更し、処理を予約するだけにしてUIスレッドをブロックしないようにする
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
          ViewModel?.OnTimerTick(TimeSpan.FromMilliseconds(e.Time));
        });
      };

      // 再生タイマーは、もはや再生位置の通知には不要だが、UI更新の補助として残しても良い
      _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
    }

    private void HandlePlaybackAction(PlaybackAction action, ClipViewModel? clipToPlay)
    {
      switch (action)
      {
        case PlaybackAction.Pause:
          _mediaPlayer.Pause();
          break;
        case PlaybackAction.Stop:
          _mediaPlayer.Stop();
          break;
      }
    }

    private void HandleSeekRequest(ClipViewModel clip, TimeSpan positionInClip, bool isPlaying)
    {
      _resumePlaybackAfterSeek = isPlaying;
      _pendingSeekPosition = clip.TrimStart + ((positionInClip == TimeSpan.MinValue) ? TimeSpan.Zero : positionInClip);

      // 既に同じクリップが再生中の場合は、シークのみ行う
      if (_currentClipInPlayer == clip && _mediaPlayer.IsPlaying)
      {
        _mediaPlayer.Time = (long)_pendingSeekPosition.Value.TotalMilliseconds;
        if (isPlaying) _mediaPlayer.Play();
        return;
      }

      _currentClipInPlayer = clip;

      // 新しいMediaオブジェクトを作成して再生を開始
      var media = new Media(_libVLC, new Uri(clip.FilePath));
      _mediaPlayer.Media = media;

      // 再生開始位置を設定してから再生
      _mediaPlayer.Time = (long)_pendingSeekPosition.Value.TotalMilliseconds;
      if (isPlaying) _mediaPlayer.Play();
    }

    private void Timeline_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      // クリックされたUI要素から、対応するClipViewModelを取得
      var clickedClipVM = (e.OriginalSource as FrameworkElement)?.DataContext as ClipViewModel;

      // ViewModelに「操作が開始されたこと」を、情報と共に伝える
      ViewModel?.StartInteraction(clickedClipVM, e.GetPosition(sender as IInputElement));
      (sender as UIElement)?.CaptureMouse();
    }

    private void Timeline_MouseMove(object sender, MouseEventArgs e)
    {
      // ViewModelに「マウスが動いたこと」を伝える
      ViewModel?.UpdateInteraction(e.GetPosition(sender as IInputElement));
    }

    private void Timeline_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
      // ViewModelに「操作が終了したこと」を伝える
      ViewModel?.EndInteraction();
      (sender as UIElement)?.ReleaseMouseCapture();
    }

    private void TimelineBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      double clickedX = e.GetPosition(sender as IInputElement).X;
      TimeSpan clickedTime = TimeSpan.FromSeconds(clickedX / Config.PixelsPerSecond);
      ViewModel.SeekToTime(clickedTime);
    }

    // アプリケーション終了時にリソースを解放
    public void Dispose()
    {
      _mediaPlayer.Dispose();
      _libVLC.Dispose();
    }
  }
}