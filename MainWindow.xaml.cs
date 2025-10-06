using A23_MVVM;
using LibVLCSharp.Shared;
using System;
using System.Threading; // ★★★ Interlockedクラスのために追加 ★★★
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using static A23_MVVM.MainWindowViewModel;

namespace A23_MVVM
{
  public partial class MainWindow : Window, IDisposable
  {
    private LibVLC _libVLC;
    private LibVLCSharp.Shared.MediaPlayer _mediaPlayer;
    private DispatcherTimer _playbackTimer;
    private ClipViewModel? _currentClipInPlayer = null;

    private long _latestPlayerPositionTicks = 0;

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
      _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
      PreviewPlayer.MediaPlayer = _mediaPlayer;

      viewModel.PlaybackActionRequested += HandlePlaybackAction;
      viewModel.SeekRequested += HandleSeekRequest;
      _mediaPlayer.EncounteredError += (s, e) => MessageBox.Show("再生エラーが発生しました。");
      _mediaPlayer.TimeChanged += (s, e) => Interlocked.Exchange(ref _latestPlayerPositionTicks, e.Time * TimeSpan.TicksPerMillisecond);

      // ★★★ここからが修正箇所★★★

      // UI更新タイマーの優先度を "Input" (入力) よりも低い "Background" に設定
      _playbackTimer = new DispatcherTimer(DispatcherPriority.Background)
      {
        Interval = TimeSpan.FromMilliseconds(30)
      };

      // タイマーの処理を書き換える
      _playbackTimer.Tick += (s, e) =>
      {
        var currentTicks = Interlocked.Read(ref _latestPlayerPositionTicks);
        var currentTime = TimeSpan.FromTicks(currentTicks);

        // 1. ViewModelには、ロジック判断に必要な再生時間だけを報告
        viewModel.OnTimerTick(currentTime);

        // 2. UIの更新は、Viewが直接行う (データバインディングを経由しない)
        if (viewModel.IsPlaying && viewModel.Clips.Any())
        {
          var currentClip = viewModel.SortedClips.ElementAtOrDefault(viewModel.CurrentClipIndex);
          if (currentClip != null)
          {
            var progressWithinClip = currentTime - currentClip.TrimStart;
            var newPosition = currentClip.TimelinePosition + progressWithinClip.TotalSeconds * Config.PixelsPerSecond;

            // XAMLで名前を付けたRectangleのRenderTransformを直接更新する
            (Playhead.RenderTransform as TranslateTransform).X = newPosition;
            viewModel.PlayheadPosition = newPosition;
          }
        }
      };
    }

    private void HandlePlaybackAction(PlaybackAction action, ClipViewModel? clipToPlay)
    {
      switch (action)
      {
        case PlaybackAction.Pause:
          // プレイヤーが実際に再生中の場合のみ、Pauseを呼び出す
          if (_mediaPlayer.State == VLCState.Playing)
          {
            _mediaPlayer.Pause();
          }
          _playbackTimer.Stop();
          break;
        case PlaybackAction.Stop:
          _mediaPlayer.Stop();
          _playbackTimer.Stop();
          break;
      }
    }

    private void HandleSeekRequest(ClipViewModel clip, TimeSpan positionInClip, bool isPlaying)
    {
      _resumePlaybackAfterSeek = isPlaying;
      _pendingSeekPosition = clip.TrimStart + ((positionInClip == TimeSpan.MinValue) ? TimeSpan.Zero : positionInClip);

      if (_currentClipInPlayer == clip && _mediaPlayer.IsPlaying)
      {
        _mediaPlayer.Time = (long)_pendingSeekPosition.Value.TotalMilliseconds;
        if (isPlaying)
        {
          _mediaPlayer.Play();
          _playbackTimer.Start();
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
        _playbackTimer.Start();
      }
    }

    private void Timeline_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      var clickedClipVM = (e.OriginalSource as FrameworkElement)?.DataContext as ClipViewModel;
      ViewModel?.StartInteraction(clickedClipVM, e.GetPosition(sender as IInputElement));
      (sender as UIElement)?.CaptureMouse();
    }

    private void Timeline_MouseMove(object sender, MouseEventArgs e)
    {
      ViewModel?.UpdateInteraction(e.GetPosition(sender as IInputElement));
    }

    private void Timeline_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
      ViewModel?.EndInteraction();
      (sender as UIElement)?.ReleaseMouseCapture();
    }

    private void TimelineBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      double clickedX = e.GetPosition(sender as IInputElement).X;
      TimeSpan clickedTime = TimeSpan.FromSeconds(clickedX / Config.PixelsPerSecond);
      ViewModel.SeekToTime(clickedTime);
    }

    public void Dispose()
    {
      _mediaPlayer.Dispose();
      _libVLC.Dispose();
    }
  }
}