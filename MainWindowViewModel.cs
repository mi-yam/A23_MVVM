// MainWindowViewModel.cs
using A23_MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO; // System.IOを追加
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
    public event Action<ClipViewModel, TimeSpan, bool>? SeekRequested;

    // --- メンバ変数 ---
    private List<ClipViewModel> _sortedClips = new List<ClipViewModel>();
    private int _currentClipIndex = 0;
    private bool _isDragging = false;
    private Point _startMousePosition;
    private double _dragStartLeft;
    private bool _isInteracting = false;
    public MediaPlayer MediaPlayer { get; }

    private LibVLC _libVLC;
    // --- コンストラクタ ---
    public MainWindowViewModel()
    {
      // アプリケーション起動時に一度だけ実行される
      // FFmpegの実行ファイルがなければダウンロードする
      // InitializeFFmpeg(); // この行は必要に応じてコメントアウトまたは実装

      // VLCコアを初期化
      // Core.Initialize()はViewのUIスレッドで呼ばれるのが望ましい場合があるため、
      // App.xaml.csなどで一度だけ呼び出すのがより安全です。
      // ここでは簡潔さのためにViewModelに残します。
      Core.Initialize();

      // LibVLCとMediaPlayerのインスタンスを作成
      _libVLC = new LibVLC();
      MediaPlayer = new MediaPlayer(_libVLC);

      // --- Window_Loadedからロジックをこちらに移動 ---
      // 再生したい動画ファイルのパスやURLを指定
      var media = new Media(_libVLC, new Uri("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"));

      // メディアをプレイヤーにセットして再生開始
      MediaPlayer.Media = media;
      MediaPlayer.Play();
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

    public void EndInteraction()
    {
      if (!_isInteracting || SelectedClip == null) return;

      // Canva方式の再整列ロジック
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
      if (currentVideoPosition >= currentClip.TrimStart + currentClip.Duration)
      {
        GoToNextClip();
        return; // 次のクリップの処理に移るので、ここで処理を抜ける
      }
      // 再生位置からクリップの開始時間を引くことで、クリップ内での再生経過時間を算出
      var progressWithinClip = currentVideoPosition - currentClip.TrimStart;

      // タイムラインの赤い線を正しい位置に表示するための計算
      PlayheadPosition = currentClip.TimelinePosition + progressWithinClip.TotalSeconds * Config.PixelsPerSecond;

    }


    public void GoToNextClip()
    {
      _currentClipIndex++;
      if (_currentClipIndex < _sortedClips.Count)
      {
        var nextClip = _sortedClips[_currentClipIndex];

        SeekRequested?.Invoke(nextClip, TimeSpan.MinValue, true);

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
      //MessageBox.Show("MediaOpenedイベントが実行されました！");

      if (IsPlaying)
      {
        // 再生準備がまだできていない場合（＝最初の再生時）
        if (!_sortedClips.Any())
        {
          PreparePlayback();
        }
        //再生順に並べたクリップのリスト (_sortedClips) から、
        //今再生すべき順番 (_currentClipIndex) にあるクリップを取り出してください。
        //ただし、もし最後のクリップも再生し終わっていて、もう次のクリップが存在しない場合は、
        //エラーにせず、代わりに nullを clipToPlay に入れてください
        var clipToPlay = _sortedClips.ElementAtOrDefault(_currentClipIndex);

        if (clipToPlay != null)
        {
          // Viewに対して再生を要求する。
          // 現在のクリップを再生するという意図だけを伝えること。
          SeekRequested?.Invoke(clipToPlay, TimeSpan.MinValue, true);
        }

        PlayPauseButtonContent = "一時停止";
      }
      else
      {
        // 一時停止をViewに通知
        PlaybackActionRequested?.Invoke(PlaybackAction.Pause, null);
        PlayPauseButtonContent = "再生";
      }
    }

    public void SeekToTime(TimeSpan clickedTime)
    {
      PlayheadPosition = clickedTime.TotalSeconds * Config.PixelsPerSecond;
      PreparePlayback();
      var sortedClips = _sortedClips;
      TimeSpan cumulativeTime = TimeSpan.Zero;

      foreach (var clip in sortedClips)
      {
        if (clickedTime <= cumulativeTime + clip.Duration)
        {
          int newIndex = sortedClips.IndexOf(clip);
          _currentClipIndex = newIndex;

          TimeSpan positionInClip = clickedTime - cumulativeTime;
          SeekRequested?.Invoke(clip, positionInClip, IsPlaying);

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
    // in MainWindowViewModel.cs

    [RelayCommand]
    private void SplitClip()
    {
      // 1. 分割対象のクリップとクリップ内での分割時間を特定
      var playheadTime = TimeSpan.FromSeconds(PlayheadPosition / Config.PixelsPerSecond);
      var sortedClips = Clips.OrderBy(c => c.TimelinePosition).ToList();
      ClipViewModel? targetClip = null;
      TimeSpan cumulativeTime = TimeSpan.Zero;

      foreach (var clip in sortedClips)
      {
        if (playheadTime >= cumulativeTime && playheadTime < cumulativeTime + clip.Duration)
        {
          targetClip = clip;
          break;
        }
        cumulativeTime += clip.Duration;
      }
      if (targetClip == null) return;

      var splitTimeInClip = playheadTime - cumulativeTime;
      if (splitTimeInClip <= TimeSpan.Zero || splitTimeInClip >= targetClip.Duration) return;

      // 2. 元クリップの元の長さを、変更前に保存しておく
      var originalDuration = targetClip.Duration;
      targetClip.OriginalDuration = originalDuration;

      // 3. 新しいクリップ（後半部分）の「設計図」を作成する
      var newClipModel = new VideoClip
      {
        FilePath = targetClip.FilePath,
        TrimStart = targetClip.TrimStart + splitTimeInClip,
        Duration = originalDuration - splitTimeInClip,
      };
      var newClipViewModel = new ClipViewModel(newClipModel);

      //    元のクリップ（前半部分）の「設計図」を完全に更新する
      targetClip.Model.Duration = splitTimeInClip; // ModelのDurationを更新
      targetClip.Duration = splitTimeInClip;       // ViewModelのDurationを更新
      targetClip.Width = splitTimeInClip.TotalSeconds * Config.PixelsPerSecond; // UIの幅も更新

      // 5. 新しいクリップをリストに追加し、タイムラインを再整列
      int targetIndex = Clips.IndexOf(targetClip);
      Clips.Insert(targetIndex + 1, newClipViewModel);

      double currentPosition = 0;
      foreach (var clip in Clips.OrderBy(c => c.TimelinePosition))
      {
        clip.TimelinePosition = currentPosition;
        clip.Model.TimelinePosition = currentPosition;
        currentPosition += clip.Width;
      }
    }
  }
  }