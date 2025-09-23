// MainWindowViewModel.cs
using A23_MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using static A23_MVVM.MainWindow;

namespace A23_MVVM // あなたのプロジェクト名に合わせてください
{
  public enum PlaybackAction { Play, Pause, Stop }
  public partial class MainWindowViewModel : ObservableObject
  {
    // --- プロパティ ---
    public ObservableCollection<ClipViewModel> Clips { get; } = new ObservableCollection<ClipViewModel>();
    private List<ClipViewModel> _swappedClips = new List<ClipViewModel>(); 

    [ObservableProperty]
    private string _playPauseButtonContent = "再生";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _playheadPosition;

    [ObservableProperty]
    private ClipViewModel? _selectedClip;

    // --- イベント ---
    public event Action<PlaybackAction, ClipViewModel?>? PlaybackActionRequested;
    public event Action<ClipViewModel, TimeSpan>? SeekRequested;

    // --- メンバ変数 ---
    private List<ClipViewModel> _sortedClips = new List<ClipViewModel>();
    private int _currentClipIndex = 0;
    private bool _isDragging = false;
    private Point _startMousePosition;
    private double _dragStartLeft;
    private bool _isInteracting = false;

    // --- コンストラクタ ---
    public MainWindowViewModel()
    {
      // アプリケーション起動時に一度だけ実行される
      // FFmpegの実行ファイルがなければダウンロードする
      InitializeFFmpeg();
    }

    private async void InitializeFFmpeg()
    {
      await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official);
    }

    // --- コマンド ---

    [RelayCommand]
    private async Task OpenFile()
    {
      var openFileDialog = new OpenFileDialog
      {
        Title = "動画ファイルを選択",
        Filter = "動画ファイル|*.mp4;*.mov;*.wmv;*.avi|全てのファイル|*.*",
        RestoreDirectory = true
      };

      if (openFileDialog.ShowDialog() == true)
      {
        try
        {
          // 1. Xabe.FFmpegを使って動画の情報を取得する (より簡単で確実)
          IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(openFileDialog.FileName);
          TimeSpan originalDuration = mediaInfo.Duration;

          // 2. Model（純粋なデータ）を作成
          var newClipModel = new A23_MVVM.VideoClip
          {
            FilePath = openFileDialog.FileName,
            Duration = originalDuration // 初期値は同じ
          };

          // 3. Modelを元に、UI表示用のViewModelを作成
          var newClipViewModel = new ClipViewModel(newClipModel);

          // 4. 新しいクリップの配置位置を計算する
          double maxRight = Clips.Any()
              ? Clips.Max(clip => clip.TimelinePosition + clip.Width)
              : 0;
          newClipViewModel.TimelinePosition = maxRight;

          // 5. タイムラインのコレクションに新しいClipViewModelを追加する
          // ObservableCollectionなので、UIは自動的に更新される
          Clips.Add(newClipViewModel);
        }
        catch (Exception ex)
        {
          MessageBox.Show("ファイルの読み込みに失敗しました。\n" + ex.Message);
        }
      }
    }

    // --- コマンド ---
    [RelayCommand]
    private void DeselectAll()
    {
      if (SelectedClip != null)
      {
        SelectedClip.IsSelected = false;
        SelectedClip = null;
      }
    }

    // --- Viewから呼び出されるメソッド群 ---
    public void StartInteraction(ClipViewModel? clickedClip, Point mousePositionOnTimeline)
    {
      // 1. まずは全ての選択を解除する
      _isInteracting = true;
      DeselectAll();
      _swappedClips.Clear(); // これを追加

      // 2. 何もない場所がクリックされた場合は、ここで終了
      if (clickedClip == null) return;

      // 3. クリックされたクリップを選択状態にする
      clickedClip.IsSelected = true;
      SelectedClip = clickedClip;

      // 4. マウスの位置を元に、ドラッグかトリミングかを判断し、準備する
      Point mousePositionOnClip = new Point(
          mousePositionOnTimeline.X - clickedClip.TimelinePosition,
          0 // Y座標は今回は無関係
      );
       _isDragging = true;

      // 5. 操作前の状態をスナップショットとして保存
      _startMousePosition = mousePositionOnTimeline;
      _dragStartLeft = clickedClip.TimelinePosition;
    }

    // UpdateInteractionメソッドを以下のように書き換える
    public void UpdateInteraction(Point currentMousePosition)
    {
      if (!_isInteracting || SelectedClip == null) return;

      double deltaX = currentMousePosition.X - _startMousePosition.X;

      double newLeft = _dragStartLeft + deltaX;
      if (newLeft < 0)
      {
        newLeft = 0;
      }
      SelectedClip.TimelinePosition = newLeft;
    }

    // EndInteractionメソッドを以下のように書き換える
    public void EndInteraction()
    {
      if (!_isInteracting || SelectedClip == null) return;

      // Canva方式の再整列ロジック（これはそのまま残す）
      var sortedClips = Clips.OrderBy(c => c.TimelinePosition).ToList();
      double currentPosition = 0;
      foreach (var clip in sortedClips)
      {
        clip.TimelinePosition = currentPosition;
        currentPosition += clip.Width;
      }

      // ModelのTimelinePositionだけ更新すればOK
      foreach (var clip in Clips)
      {
        clip.Model.TimelinePosition = clip.TimelinePosition;
      }

      _isInteracting = false;
    }


    public void OnTimerTick(TimeSpan currentVideoPosition)
    {
      if (!IsPlaying || !_sortedClips.Any() || _currentClipIndex >= _sortedClips.Count) return;

      var currentClip = _sortedClips[_currentClipIndex];

      // 再生ヘッドの位置を更新する処理「だけ」を行う
      // TrimStartは削除済みなので、currentVideoPositionを直接使う
      double currentClipProgress = currentVideoPosition.TotalSeconds * Config.PixelsPerSecond;
      PlayheadPosition = currentClip.TimelinePosition + currentClipProgress;
    }


    public void GoToNextClip()
    {
      _currentClipIndex++;
      if (_currentClipIndex < _sortedClips.Count)
      {
        var nextClip = _sortedClips[_currentClipIndex];
        PlaybackActionRequested?.Invoke(PlaybackAction.Play, nextClip);
      }
      else
      {
        IsPlaying = false;
        PlaybackActionRequested?.Invoke(PlaybackAction.Stop, null);
        PlayheadPosition = 0;
        PlayPauseButtonContent = "再生";
        _sortedClips.Clear();
        _currentClipIndex = 0;
      }
    }

    [RelayCommand]
    private void PlayPause()
    {
      IsPlaying = !IsPlaying;

      if (IsPlaying)
      {
        if (_currentClipIndex == 0 && !_sortedClips.Any())
        {
          PreparePlayback();
        }
        var clipToPlay = _sortedClips.ElementAtOrDefault(_currentClipIndex);
        PlaybackActionRequested?.Invoke(PlaybackAction.Play, clipToPlay);
        PlayPauseButtonContent = "一時停止";
      }
      else
      {
        PlaybackActionRequested?.Invoke(PlaybackAction.Pause, null);
        PlayPauseButtonContent = "再生";
      }
    }

    // SeekToTimeメソッドを以下のように書き換える
    public void SeekToTime(TimeSpan clickedTime)
    {
      PlayheadPosition = clickedTime.TotalSeconds * Config.PixelsPerSecond;
      PreparePlayback();
      var sortedClips = _sortedClips;
      TimeSpan cumulativeTime = TimeSpan.Zero;

      foreach (var clip in sortedClips)
      {
        // 許容誤差を持たせた判定
        if (clickedTime <= cumulativeTime + clip.Duration)
        {
          int newIndex = sortedClips.IndexOf(clip);
          _currentClipIndex = newIndex;
          TimeSpan positionInClip = clickedTime - cumulativeTime;
          SeekRequested?.Invoke(clip, positionInClip);
          return;
        }
        cumulativeTime += clip.Duration;
      }
    }
    // --- 再生ロジックのメソッド群 ---
    private void PreparePlayback()
    {
      _sortedClips = Clips.OrderBy(c => c.TimelinePosition).ToList();
      _currentClipIndex = 0;
    }


  }
}