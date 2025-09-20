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

    [ObservableProperty]
    private string _playPauseButtonContent = "再生";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _playheadPosition;

    [ObservableProperty]
    private ClipViewModel? _selectedClip;

    // --- イベント ---
    // ★★★ 変更点2：Viewに再生するクリップも渡せるようにイベントの型を変更 ★★★
    public event Action<PlaybackAction, ClipViewModel?>? PlaybackActionRequested;

    // --- メンバ変数 ---
    private List<ClipViewModel> _sortedClips = new List<ClipViewModel>();
    private int _currentClipIndex = 0;
    private bool _isDragging = false;
    private TrimMode _trimMode = TrimMode.None;
    private Point _startMousePosition;
    private double _dragStartLeft;
    private double _dragStartWidth;
    private TimeSpan _dragStartDuration;
    private TimeSpan _dragStartTrimStart;

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
            OriginalDuration = originalDuration,
            Duration = originalDuration, // 初期値は同じ
            TrimStart = TimeSpan.Zero
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
      if (_selectedClip != null)
      {
        _selectedClip.IsSelected = false;
        _selectedClip = null;
      }
    }

    // --- Viewから呼び出されるメソッド群 ---
    public void StartInteraction(ClipViewModel? clickedClip, Point mousePositionOnTimeline)
    {
      // 1. まずは全ての選択を解除する
      DeselectAll();

      // 2. 何もない場所がクリックされた場合は、ここで終了
      if (clickedClip == null) return;

      // 3. クリックされたクリップを選択状態にする
      clickedClip.IsSelected = true;
      _selectedClip = clickedClip;

      // 4. マウスの位置を元に、ドラッグかトリミングかを判断し、準備する
      Point mousePositionOnClip = new Point(
          mousePositionOnTimeline.X - clickedClip.TimelinePosition,
          0 // Y座標は今回は無関係
      );

      if (mousePositionOnClip.X <= Config.TrimHandleWidth)
      {
        _trimMode = TrimMode.Left;
      }
      else if (mousePositionOnClip.X >= clickedClip.Width - Config.TrimHandleWidth)
      {
        _trimMode = TrimMode.Right;
      }
      else
      {
        _isDragging = true;
      }

      // 5. 操作前の状態をスナップショットとして保存
      _startMousePosition = mousePositionOnTimeline;
      _dragStartLeft = clickedClip.TimelinePosition;
      _dragStartWidth = clickedClip.Width;
      _dragStartTrimStart = clickedClip.TrimStart;
      _dragStartDuration = clickedClip.Duration;
    }

    public void UpdateInteraction(Point currentMousePosition)
    {
      if (_selectedClip == null) return;

      double deltaX = currentMousePosition.X - _startMousePosition.X;

      if (_isDragging)
      {
        // ドラッグ中の処理
        _selectedClip.TimelinePosition = _dragStartLeft + deltaX;
        // TODO: スナッピングロジックもここに追加できる
      }
      else if (_trimMode == TrimMode.Right)
      {
        // 右トリミング中の処理
        double newWidth = _dragStartWidth + deltaX;
        if (newWidth < Config.MinClipWidth) newWidth = Config.MinClipWidth;
        _selectedClip.Width = newWidth;
      }
      else if (_trimMode == TrimMode.Left)
      {
        // 左トリミング中の処理
        double newWidth = _dragStartWidth - deltaX;
        double newLeft = _dragStartLeft + deltaX;
        if (newWidth < Config.MinClipWidth)
        {
          newWidth = _dragStartWidth; // 幅は変えずに位置だけ変える
          newLeft = _dragStartLeft;
        }
        _selectedClip.Width = newWidth;
        _selectedClip.TimelinePosition = newLeft;
      }
    }

    public void EndInteraction()
    {
      if (_selectedClip != null)
      {
        // データを更新
        if (_isDragging || _trimMode != TrimMode.None)
        {
          // UIの値を元に、データモデルの値を最終確定させる
          var model = _selectedClip.Model;
          model.TimelinePosition = _selectedClip.TimelinePosition;

          var newDuration = TimeSpan.FromSeconds(_selectedClip.Width / Config.PixelsPerSecond);
          model.Duration = newDuration;

          if (_trimMode == TrimMode.Left)
          {
            double deltaX = _selectedClip.TimelinePosition - _dragStartLeft;
            var deltaTime = TimeSpan.FromSeconds(deltaX / Config.PixelsPerSecond);
            model.TrimStart = _dragStartTrimStart + deltaTime;
          }
        }
      }

      // 全てのモードをリセット
      _isDragging = false;
      _trimMode = TrimMode.None;
    }

    // --- コマンドとメソッドの更新 ---
    [RelayCommand]
    private void PlayPause()
    {
      // 自身の再生状態を反転させる
      IsPlaying = !IsPlaying;

      if (IsPlaying)
      {
        if (_currentClipIndex == 0 && !_sortedClips.Any())
        {
          PreparePlayback();
        }

        // 現在再生すべきクリップを取得して、Viewに再生を依頼する
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
    // --- 再生ロジックのメソッド群 ---
    private void PreparePlayback()
    {
      _sortedClips = Clips.OrderBy(c => c.TimelinePosition).ToList();
      _currentClipIndex = 0;
    }

    // TimerのTickイベントから呼び出されるメソッド
    public void OnTimerTick(TimeSpan currentVideoPosition)
    {
      if (!IsPlaying || !_sortedClips.Any() || _currentClipIndex >= _sortedClips.Count) return;

      var currentClip = _sortedClips[_currentClipIndex];

      // クリップの終了判定
      if (currentVideoPosition >= currentClip.TrimStart + currentClip.Duration)
      {
        GoToNextClip();
      }
      else
      {
        // 再生ヘッドの位置を計算して更新
        // ViewModelはCanvasを知らないので、クリップのデータ(TimelinePosition)を基準に計算
        double basePosition = currentClip.TimelinePosition;
        double currentClipProgress = (currentVideoPosition - currentClip.TrimStart).TotalSeconds * Config.PixelsPerSecond;
        PlayheadPosition = basePosition + currentClipProgress;
      }
    }

    // 次のクリップへ移動するロジック
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

    // （PlayClipAtIndexメソッドはViewModel内に移動 or 新設）
    public void PlayClipAtIndex(int index)
    {
      
    }
  }
}