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
    private ClipViewModel? _currentClipInPlayer = null; // この行を追加


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
        case PlaybackAction.Pause:
          PreviewPlayer.Pause();
          _playbackTimer.Stop();
          break;

        case PlaybackAction.Stop:
          PreviewPlayer.Stop();
          _playbackTimer.Stop();
          PreviewPlayer.Close();
          break;
      }
    }

    // in MainWindow.xaml.cs

    private void HandleSeekRequst(ClipViewModel clip, TimeSpan positionInClip, bool isPlaying)
    {
      // 要求されたクリップが現在読み込まれているものと違う場合、ソースを入れ替える
      if (_currentClipInPlayer != clip || PreviewPlayer.Source == null)
      {
        _currentClipInPlayer = clip;
        _resumePlaybackAfterSeek = isPlaying;
        PreviewPlayer.Close(); // 古いメディアを閉じる

        // もし再生再開（MinValue）なら新しいクリップの先頭から、そうでなければ指定された位置から
        _pendingSeekPosition = (positionInClip == TimeSpan.MinValue) ? TimeSpan.Zero : positionInClip;

        PreviewPlayer.Source = new Uri(clip.FilePath);
        // この後の再生処理は PreviewPlayer_MediaOpened イベントハンドラに任せる
      }
      else
      {
        // 正しいクリップが既に読み込まれている場合（シークまたは再生再開）
        // 特定の再生位置が指定されていれば、そこにシークする
        if (positionInClip != TimeSpan.MinValue)
        {
          PreviewPlayer.Position = positionInClip;
        }

        // 再生フラグがtrueなら再生を開始する
        if (isPlaying)
        {
          PreviewPlayer.Play();
          _playbackTimer.Start();
        }
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