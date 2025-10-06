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
      Core.Initialize();
      InitializeComponent();

      var viewModel = new MainWindowViewModel();
      DataContext = viewModel;

      _libVLC = new LibVLC();
      _mediaPlayer = new MediaPlayer(_libVLC);
      PreviewPlayer.MediaPlayer = _mediaPlayer;

      viewModel.PlaybackActionRequested += HandlePlaybackAction;
      viewModel.SeekRequested += HandleSeekRequest;
      _mediaPlayer.EncounteredError += (s, e) => MessageBox.Show("再生エラーが発生しました。");

      // ★★★修正点1：TimeChangedは直接UIを更新せず、ViewModelの変数に値を渡すだけにする★★★
      _mediaPlayer.TimeChanged += (s, e) => ViewModel.CurrentPlayerPosition = TimeSpan.FromMilliseconds(e.Time);

      // ★★★修正点2：UI更新専用のタイマーをセットアップ★★★
      _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) }; // 約30fpsでUIを更新
      _playbackTimer.Tick += (s, e) => ViewModel.OnTimerTick(); // タイマーがViewModelのUI更新メソッドを呼び出す
    }

    private void HandlePlaybackAction(PlaybackAction action, ClipViewModel? clipToPlay)
    {
      switch (action)
      {
        case PlaybackAction.Pause:
          _mediaPlayer.Pause();
          _playbackTimer.Stop(); // ★タイマーを停止
          break;
        case PlaybackAction.Stop:
          _mediaPlayer.Stop();
          _playbackTimer.Stop(); // ★タイマーを停止
          break;
      }
    }

    private void HandleSeekRequest(ClipViewModel clip, TimeSpan positionInClip, bool isPlaying)
    {
      // ... (既存のコードは変更なし)
      _resumePlaybackAfterSeek = isPlaying;
      _pendingSeekPosition = clip.TrimStart + ((positionInClip == TimeSpan.MinValue) ? TimeSpan.Zero : positionInClip);

      if (_currentClipInPlayer == clip && _mediaPlayer.IsPlaying)
      {
        _mediaPlayer.Time = (long)_pendingSeekPosition.Value.TotalMilliseconds;
        if (isPlaying)
        {
          _mediaPlayer.Play();
          _playbackTimer.Start(); // ★タイマーを開始
        }
        return;
      }

      _currentClipInPlayer = clip;
      var media = new Media(_libVLC, new Uri(clip.FilePath));
      _mediaPlayer.Media = media;

      _mediaPlayer.Time = (long)_pendingSeekPosition.Value.TotalMilliseconds;
      if (isPlaying)
      {
        _mediaPlayer.Play();
        _playbackTimer.Start(); // ★タイマーを開始
      }
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