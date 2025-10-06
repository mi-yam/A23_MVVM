// in MainWindow.xaml.cs

// 1. 必要なusingディレクティブを追加
using System;
using System.Windows;
using System.Windows.Threading;
using LibVLCSharp.Shared; // VLCライブラリのコア
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
      _mediaPlayer.TimeChanged += (s, e) => ViewModel?.OnTimerTick(TimeSpan.FromMilliseconds(e.Time));

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

    // アプリケーション終了時にリソースを解放
    public void Dispose()
    {
      _mediaPlayer.Dispose();
      _libVLC.Dispose();
    }
  }
}