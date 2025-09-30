using A23_MVVM;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static A23_MVVM.MainWindowViewModel;

namespace A23_MVVM
{
  public partial class MainWindow : Window
  {

    //マウス関係の処理
    private bool isDragging = false;
    // マウス操作の開始時の状態を記憶しておくための変数
    private Point _dragStartMousePosition;
    private double _dragStartLeft;
    private double _dragStartWidth;
    private TimeSpan _dragStartTrimStart;
    private TimeSpan _dragStartDuration;

    private Point mouseOffset;
    private FrameworkElement? targetElement = null;
    private Border? _selectedClipUI = null; // 現在選択されているクリップのUI

    //プレビュー関係
    private DispatcherTimer _playbackTimer;
    private List<VideoClip> _timelineClips = [];
    private List<VideoClip> _sortedClips = []; // 再生順に並べたクリップのリスト
    private int _currentClipIndex = 0;      // 現在再生中のクリップのインデックス

    //シーク関係
    private TimeSpan? _pendingSeekPosition = null;
    private bool _resumePlaybackAfterSeek = false;

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow()
    {
      InitializeComponent();
      var viewModel = new MainWindowViewModel();
      DataContext = viewModel;

      // イベントの購読
      viewModel.PlaybackActionRequested += HandlePlaybackAction;
      viewModel.SeekRequested += HandleSeekRequst;

      // タイマーのセットアップ
      _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
      _playbackTimer.Tick += PlaybackTimer_Tick;
      PreviewPlayer.MediaOpened += PreviewPlayer_MediaOpened;
    }

    // タイマーは、ViewModelに現在の再生時間を通知するだけ
    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
      ViewModel?.OnTimerTick(PreviewPlayer.Position);
    }

    private void HandlePlaybackAction(PlaybackAction action, ClipViewModel? clipToPlay)
    {
      switch (action)
      {
        case PlaybackAction.Play:
          if (clipToPlay != null)
          {
            // 再生ボタンからの再生は、常に「現在の位置から」なので、positionInClipはPreviewPlayer.Positionを渡す
            HandleSeekRequst(clipToPlay, PreviewPlayer.Position, true);
          }
          else if (ViewModel.IsPlaying) // clipToPlayがnullでも、再生状態なら再開
          {
            PreviewPlayer.Play();
            _playbackTimer.Start();
          }
          break;

        case PlaybackAction.Pause:
          PreviewPlayer.Pause();
          _playbackTimer.Stop();
          break;

        case PlaybackAction.Stop:
          PreviewPlayer.Stop();
          _playbackTimer.Stop();
          PreviewPlayer.Close(); // 停止時はCloseしてリソースを解放するのが望ましい
          break;
      }
    }
    private void PreviewPlayer_MediaEnded(object sender, RoutedEventArgs e)
    {
      // 再生が終わったことをViewModelに報告するだけ
      ViewModel?.GoToNextClip();
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

    private void TimeLine_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      // タイムラインのどこかをクリックしたときに、選択中クリップの選択を解除する
      if (_selectedClipUI != null)
      {
        _selectedClipUI.BorderBrush = Brushes.Black;
        _selectedClipUI.BorderThickness = new Thickness(1);
        _selectedClipUI = null;
      }
    }

    private void TimelineBackground_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      double clickedX = e.GetPosition(sender as IInputElement).X;
      TimeSpan clickedTime = TimeSpan.FromSeconds(clickedX / Config.PixelsPerSecond);
      ViewModel.SeekToTime(clickedTime);
    }

    private void HandleSeekRequst(ClipViewModel clip,TimeSpan positionInClip,bool isPlaying)
    {
      // ステップ1: 「再生を再開するか」「どこに移動したいか」をメモ（予約）する
      _resumePlaybackAfterSeek = isPlaying;
      _pendingSeekPosition = positionInClip;

      // ステップ2: プレイヤーを一度リセットし、新しい動画の読み込みを「依頼」する
      PreviewPlayer.Close();
      PreviewPlayer.Source = new Uri(clip.FilePath);
    }

    private void PreviewPlayer_MediaOpened(object sender, System.Windows.RoutedEventArgs e)
    {
      // ステップ3: プレイヤーから「準備完了」の報告が来たので、予約していた作業を実行する
      if (_pendingSeekPosition.HasValue)
      {
        PreviewPlayer.Position = _pendingSeekPosition.Value;
        _pendingSeekPosition = null; // 予約を消す
      }
      if (_resumePlaybackAfterSeek)
      {
        PreviewPlayer.Play();
        _playbackTimer.Start(); // タイマーも忘れずに
        _resumePlaybackAfterSeek = false; // 予約を消す
      }
    }

  }//class MainWindow
} // namespace A23_MVVM