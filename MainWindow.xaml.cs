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
    private TimeSpan? _pendingSeekPosition = null;
    private List<VideoClip> _timelineClips = [];
    private List<VideoClip> _sortedClips = []; // 再生順に並べたクリップのリスト
    private int _currentClipIndex = 0;      // 現在再生中のクリップのインデックス


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

    // イベントハンドラのシグネチャ（引数の型）をViewModelのイベント定義に合わせる
    private void HandlePlaybackAction(PlaybackAction action, ClipViewModel? clipToPlay)
    {
      switch (action)
      {
        case PlaybackAction.Play:
          if (clipToPlay != null)
          {
            if (PreviewPlayer.Source?.OriginalString != clipToPlay.FilePath)
            {
              // MediaOpenedイベントでPositionが設定されるのを待つ
              _pendingSeekPosition = TimeSpan.Zero;
              PreviewPlayer.Source = new Uri(clipToPlay.FilePath);
            }
            else
            {
              // ソースが同じなら、すぐに先頭へ
              PreviewPlayer.Position = TimeSpan.Zero;
            }
          }

          // 再生を開始
          PreviewPlayer.Play();
          _playbackTimer.Start();
          break;

        case PlaybackAction.Pause:
          PreviewPlayer.Pause();
          _playbackTimer.Stop();
          break;

        case PlaybackAction.Stop:
          PreviewPlayer.Stop();

          _playbackTimer.Stop();
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
      //TODO: 再生位置を更新する
    }

    private void HandleSeekRequst(ClipViewModel clip,TimeSpan positionInClip)
    {
      if (PreviewPlayer.Source?.OriginalString != clip.FilePath)
      {
        _pendingSeekPosition = positionInClip;
        PreviewPlayer.Source = new Uri(clip.FilePath);
      }
      else
      {
        PreviewPlayer.Position = positionInClip;
      }
    }

    private void PreviewPlayer_MediaOpened(object sender, System.Windows.RoutedEventArgs e) 
    {
      if(_pendingSeekPosition.HasValue)
      {
        PreviewPlayer.Position = _pendingSeekPosition.Value;
        _pendingSeekPosition = null;
      }
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
      // ViewModelのコマンドを呼び出すだけにする
      ViewModel?.PlayPauseCommand.Execute(null);
    }

  }//class MainWindow
} // namespace A23_MVVM