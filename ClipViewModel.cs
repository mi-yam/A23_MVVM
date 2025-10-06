// in ClipViewModel.cs
using A23_MVVM;
using CommunityToolkit.Mvvm.ComponentModel;

namespace A23_MVVM 
{
  public partial class ClipViewModel : ObservableObject
  {
    // Model（生の食材）
    public VideoClip Model { get; }

    [ObservableProperty]
    private string _filePath;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private TimeSpan _originalDuration;

    [ObservableProperty]
    private TimeSpan _trimStart;
    [ObservableProperty]
    private TimeSpan _trimEnd;

    // --- UIの状態を表すプロパティ ---
    [ObservableProperty]
    private double _width;

    [ObservableProperty]
    private double _timelinePosition;

    [ObservableProperty]
    private bool _isSelected;

    // コンストラクタ
    public ClipViewModel(VideoClip model)
    {
      Model = model;

      // ModelのデータをViewModelのプロパティにコピー
      _filePath = model.FilePath;
      _duration = model.Duration;
      _trimStart = model.TrimStart;
      // Modelのデータを元に、UI用のプロパティを初期化
      _width = model.Duration.TotalSeconds * Config.PixelsPerSecond;
      _timelinePosition = model.TimelinePosition;
      _isSelected = false;
    }
  }
}