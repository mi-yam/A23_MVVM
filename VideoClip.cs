using System;
using System.Windows.Controls;

namespace A23_MVVM
{
  public class VideoClip
  {
    public string FilePath { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan OriginalDuration { get; set; }
    public TimeSpan TrimStart { get; set; }
    public Border ClipUI { get; set; } // ★これは後で削除します
    public double TimelinePosition { get; set; }
  }
}

