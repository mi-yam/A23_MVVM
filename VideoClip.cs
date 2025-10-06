using System;
using System.Windows.Controls;

namespace A23_MVVM
{
  public class VideoClip
  {
    public required  string FilePath { get; set; }
    public TimeSpan Duration { get; set; }
    public double TimelinePosition { get; set; }
    public TimeSpan TrimStart { get; set; }
  }
}

