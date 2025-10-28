using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LibVLCSharp.Shared;
using System.Collections.Generic; // IEnumerable<T> のため
using System.Linq; // Enumerable.Empty<T> のため

// ViewModelの基底クラスやコマンドクラスを配置する名前空間
namespace A23_MVVM
{
  // (A23_MVVM.ClipViewModel がこの名前空間に存在すると仮定)
  // (A23_MVVM.Config がこの名前空間に存在すると仮定)

  public enum PlaybackAction { Play, Pause, Stop }
  /// <summary>
  /// メインウィンドウのViewModel。
  /// LibVLCのMediaPlayerを管理し、UIとの間のデータバインディングとコマンドを提供します。
  /// </summary>
  public class MainWindowViewModel : ViewModelBase, IDisposable
  {
    #region Fields

    private readonly LibVLC _libVLC;
    private MediaPlayer _mediaPlayer;

    private bool _isPlaying;
    private bool _isPaused;

    // --- MainWindow.xaml.cs から参照されるため追加 ---
    private int _currentClipIndex;
    private double _playheadPosition;

    /// <summary>
    /// タイムラインに表示するための、ソート済みのクリップのコレクション。
    /// (ここでは空のリストを返す仮実装としています)
    /// </summary>
    public IEnumerable<ClipViewModel> SortedClips { get; private set; } = Enumerable.Empty<ClipViewModel>();

    #endregion

    #region Properties

    /// <summary>
    /// View(VideoView)にバインドするためのMediaPlayerインスタンス。
    /// </summary>
    public MediaPlayer MediaPlayer
    {
      get => _mediaPlayer;
      private set => SetProperty(ref _mediaPlayer, value);
    }

    /// <summary>
    /// 現在再生中かどうかを示すプロパティ。
    /// </summary>
    public bool IsPlaying
    {
      get => _isPlaying;
      private set
      {
        if (SetProperty(ref _isPlaying, value))
        {
          // IsPlayingプロパティの変更をトリガーに、コマンドの実行可否を再評価させる
          PlayCommand.RaiseCanExecuteChanged();
          PauseCommand.RaiseCanExecuteChanged();
          StopCommand.RaiseCanExecuteChanged();
        }
      }
    }

    // --- MainWindow.xaml.cs から参照されるため追加 ---

    /// <summary>
    /// 現在再生対象となっているクリップの、SortedClips内でのインデックス。
    /// </summary>
    public int CurrentClipIndex
    {
      get => _currentClipIndex;
      set => SetProperty(ref _currentClipIndex, value);
    }

    /// <summary>
    /// タイムライン上の再生ヘッドのX座標。(Viewから更新される)
    /// </summary>
    public double PlayheadPosition
    {
      get => _playheadPosition;
      set => SetProperty(ref _playheadPosition, value);
    }

    #endregion

    #region Commands

    public RelayCommand<string> OpenFileCommand { get; }
    public RelayCommand PlayCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand StopCommand { get; }

    #endregion

    #region Constructor

    public MainWindowViewModel()
    {
      // LibVLCの初期化 (App.xaml.csで一度だけ呼ぶのがより望ましい)
      Core.Initialize();

      _libVLC = new LibVLC();
      _mediaPlayer = new MediaPlayer(_libVLC);

      // --- コマンドの初期化 ---
      OpenFileCommand = new RelayCommand<string>(OpenFile);
      PlayCommand = new RelayCommand(Play, CanPlay);
      PauseCommand = new RelayCommand(Pause, CanPause);
      StopCommand = new RelayCommand(Stop, CanStop);

      // --- MediaPlayerイベントの購読 ---
      SubscribeToMediaPlayerEvents();
    }
    // (元のコードのコンストラクタ閉じ括弧の前にあった #region Events は構文エラーのため削除)

    #endregion // <- #region Constructor の閉じタグ

    #region Events

    /// <summary>
    /// Viewに対して再生アクションを要求するためのイベントの型を定義します。
    /// </summary>
    public delegate void PlaybackActionEventHandler(PlaybackAction action, ClipViewModel? clipToPlay);

    /// <summary>
    /// Viewに対して再生アクションを要求するためのイベント。
    /// </summary>
    public event PlaybackActionEventHandler? PlaybackActionRequested;

    // --- MainWindow.xaml.cs から参照されるため追加 ---
    // (MainWindow.xaml.cs側ではコメントアウトされていますが、もし復活させる場合は必要)
    public delegate void SeekRequestEventHandler(ClipViewModel clip, TimeSpan positionInClip, bool isPlaying);
    public event SeekRequestEventHandler? SeekRequested;

    #endregion

    #region Command Implementations

    // --- コマンドの実装例 ---

    // RelayCommandのコンストラクタで、このメソッドを渡す
    // 例: PauseCommand = new RelayCommand(RequestPause);
    private void RequestPause()
    {
      // Viewに対して「Pauseアクションをリクエストします」というイベントを発生させる
      PlaybackActionRequested?.Invoke(PlaybackAction.Pause, null);
    }

    private void RequestStop()
    {
      PlaybackActionRequested?.Invoke(PlaybackAction.Stop, null);
    }

    #endregion

    #region Public Methods (Called from View)

    // --- MainWindow.xaml.cs (View) から呼び出されるため以下を追加 ---

    /// <summary>
    /// Viewのタイマーティックに応じてViewModelの状態を更新します。
    /// </summary>
    /// <param name="currentTime">現在の再生時間</param>
    public void OnTimerTick(TimeSpan currentTime)
    {
      // TODO: 本来はここで、currentTime が現在のクリップ (SortedClips[CurrentClipIndex]) の
      // TrimEnd を超えていないかチェックし、超えていたら CurrentClipIndex を進めたり、
      // 再生を停止(Stop)または次のクリップの再生をリクエスト(SeekRequested)するロジックを実装します。

      // 例:
      // if (SortedClips.Any() && CurrentClipIndex >= 0 && CurrentClipIndex < SortedClips.Count())
      // {
      //     var currentClip = SortedClips.ElementAt(CurrentClipIndex);
      //     if (currentTime >= currentClip.TrimEnd)
      //     {
      //         // 次のクリップへ移動するか、再生を停止する
      //     }
      // }
    }

    /// <summary>
    /// タイムライン操作（クリック）の開始。
    /// </summary>
    public void StartInteraction(ClipViewModel? clickedClipVM, Point position)
    {
      // TODO: クリップのドラッグ移動や、再生ヘッドのドラッグ開始ロジックを実装
    }

    /// <summary>
    /// タイムライン操作（ドラッグ）の更新。
    /// </summary>
    public void UpdateInteraction(Point position)
    {
      // TODO: ドラッグ中のロジック（クリップや再生ヘッドの追従）を実装
    }

    /// <summary>
    /// タイムライン操作（ドロップ）の終了。
    /// </summary>
    public void EndInteraction()
    {
      // TODO: ドラッグ終了時のロジック（位置の確定など）を実装
    }

    /// <summary>
    /// 指定した時間へシーク（再生位置の移動）を要求します。
    /// </summary>
    public void SeekToTime(TimeSpan clickedTime)
    {
      // TODO: clickedTime に該当するクリップと、そのクリップ内での再生時間を計算し、
      // SeekRequested イベントを発行するロジックを実装します。

      // 例:
      // var clipToSeek = SortedClips.FirstOrDefault(c => c.TimelinePosition <= clickedTime.TotalSeconds * Config.PixelsPerSecond && ...);
      // if (clipToSeek != null)
      // {
      //     var positionInClip = clickedTime - clipToSeek.TrimStart; // (要計算)
      //     bool shouldPlay = this.IsPlaying; // 現在の再生状態を引き継ぐか
      //
      //     // View (MainWindow) にシークを要求
      //     SeekRequested?.Invoke(clipToSeek, positionInClip, shouldPlay);
      // }
    }

    #endregion


    #region Private Methods

    private void SubscribeToMediaPlayerEvents()
    {
      MediaPlayer.Playing += (s, e) => DispatcherInvoke(() => IsPlaying = true);
      MediaPlayer.Paused += (s, e) => DispatcherInvoke(() => IsPlaying = false);
      MediaPlayer.Stopped += (s, e) => DispatcherInvoke(() => IsPlaying = false);
      MediaPlayer.EndReached += (s, e) => DispatcherInvoke(() => IsPlaying = false);
      MediaPlayer.EncounteredError += (s, e) => DispatcherInvoke(() =>
      {
        IsPlaying = false;
        MessageBox.Show("エラーが発生しました。", "再生エラー", MessageBoxButton.OK, MessageBoxImage.Error);
      });
    }

    // --- Command Actions ---
    private void OpenFile(string filePath)
    {
      if (string.IsNullOrWhiteSpace(filePath)) return;

      // 以前のメディアを破棄し、新しいメディアを読み込む
      // MediaオブジェクトはIDisposableなので、使い終わったらDisposeするのが望ましい
      MediaPlayer.Media?.Dispose();
      MediaPlayer.Media = new Media(_libVLC, new Uri(filePath));

      // ファイルを開いたらすぐに再生
      Play();
    }

    private void Play() => MediaPlayer.Play();
    private void Pause() => MediaPlayer.Pause();
    private void Stop() => MediaPlayer.Stop();

    // --- Command CanExecute Conditions ---
    private bool CanPlay() => !IsPlaying && MediaPlayer.Media != null;
    private bool CanPause() => IsPlaying;
    private bool CanStop() => IsPlaying;


    /// <summary>
    /// LibVLCのイベントは別スレッドから発行される可能性があるため、
    /// UIスレッドで安全に処理を実行するためのヘルパーメソッド。
    /// </summary>
    private void DispatcherInvoke(Action action)
    {
      Application.Current?.Dispatcher.Invoke(action);
    }

    #endregion

    #region IDisposable Implementation

    private bool _disposed = false;

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (_disposed) return;

      if (disposing)
      {
        // MediaPlayerのイベント購読を解除
        // (ラムダ式で登録したため明示的な解除は難しいが、MediaPlayer自体をDisposeすれば問題ない)

        // MediaPlayerとLibVLCのリソースを解放
        MediaPlayer.Stop();
        MediaPlayer.Dispose();
        _libVLC.Dispose();
      }

      _disposed = true;
    }

    #endregion
  }

  #region Helper Classes (MVVM)

  /// <summary>
  /// INotifyPropertyChangedを実装したViewModelの基底クラス。
  /// </summary>
  public abstract class ViewModelBase : INotifyPropertyChanged
  {
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
    {
      if (Equals(storage, value)) return false;
      storage = value;
      OnPropertyChanged(propertyName);
      return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }

  /// <summary>
  /// ICommandを実装した汎用コマンドクラス。
  /// </summary>
  public class RelayCommand<T> : ICommand
  {
    private readonly Action<T> _execute;
    private readonly Predicate<T> _canExecute;

    public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
    {
      _execute = execute ?? throw new ArgumentNullException(nameof(execute));
      _canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
      add => CommandManager.RequerySuggested += value;
      remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute((T)parameter);
    public void Execute(object parameter) => _execute((T)parameter);
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
  }

  public class RelayCommand : ICommand
  {
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool> canExecute = null)
    {
      _execute = execute ?? throw new ArgumentNullException(nameof(execute));
      _canExecute = canExecute;
    }

    public event EventHandler CanExecuteChanged
    {
      add => CommandManager.RequerySuggested += value;
      remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
    public void Execute(object parameter) => _execute();
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
  }



  #endregion
}
