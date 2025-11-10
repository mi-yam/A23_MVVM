using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LibVLCSharp.Shared;
using System.Collections.Generic; // IEnumerable<T> のため
using System.Linq; // Enumerable.Empty<T> のため
using Microsoft.Win32; // OpenFileDialog のために追加

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

    // ★ LibVLC と MediaPlayer は ViewModel だけが所有します
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
    /// XAML側で {Binding MediaPlayer} として使用されます。
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

    public RelayCommand OpenFileCommand { get; } // パラメータなしに変更
    public RelayCommand PlayCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand StopCommand { get; }

    #endregion

    #region Constructor

    public MainWindowViewModel()
    {
      // LibVLCの初期化 (App.xaml.csで一度だけ呼ぶのがより望ましい)
      Core.Initialize();



      // --- コマンドの初期化 ---
      OpenFileCommand = new RelayCommand(OpenFile); // パラメータなしのメソッドを渡す
      PlayCommand = new RelayCommand(Play, CanPlay);
      PauseCommand = new RelayCommand(Pause, CanPause);
      StopCommand = new RelayCommand(Stop, CanStop);

      // --- MediaPlayerイベントの購読 ---
      SubscribeToMediaPlayerEvents();
    }

    #endregion

    #region Events

    /// <summary>
    /// Viewに対して再生アクションを要求するためのイベントの型を定義します。
    /// </summary>
    public delegate void PlaybackActionEventHandler(PlaybackAction action, ClipViewModel? clipToPlay);

    /// <summary>
    /// Viewに対して再生アクションを要求するためのイベント。
    /// </summary>
    public event PlaybackActionEventHandler? PlaybackActionRequested;


    public delegate void SeekRequestEventHandler(ClipViewModel clip, TimeSpan positionInClip, bool isPlaying);
    public event SeekRequestEventHandler? SeekRequested;

    #endregion

    #region Command Implementations

    private void RequestPause()
    {
      PlaybackActionRequested?.Invoke(PlaybackAction.Pause, null);
    }

    private void RequestStop()
    {
      PlaybackActionRequested?.Invoke(PlaybackAction.Stop, null);
    }

    #endregion

    #region Public Methods (Called from View)

    // --- MainWindow.xaml.cs (View) から呼び出されるため以下を追加 ---

    public void OnTimerTick(TimeSpan currentTime)
    {
      // TODO: クリップの終端チェックと次のクリップへの移動ロジック
    }

    public void StartInteraction(ClipViewModel? clickedClipVM, Point position)
    {
      // TODO: タイムライン操作の開始ロジック
    }

    public void UpdateInteraction(Point position)
    {
      // TODO: タイムライン操作中のロジック
    }

    public void EndInteraction()
    {
      // TODO: タイムライン操作の終了ロジック
    }

    public void SeekToTime(TimeSpan clickedTime)
    {
      // TODO: 指定時間へのシークロジック
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

    // ★ OpenFile の実装を修正 (ファイルダイアログを表示)
    private void OpenFile()
    {
      var openFileDialog = new OpenFileDialog
      {
        Title = "メディアファイルを選択",
        Filter = "メディアファイル (*.mp4;*.avi;*.mkv;*.mp3;*.wav)|*.mp4;*.avi;*.mkv;*.mp3;*.wav|すべてのファイル (*.*)|*.*"
      };

      if (openFileDialog.ShowDialog() == true)
      {
        string filePath = openFileDialog.FileName;

        // ViewModel が管理する MediaPlayer にメディアを読み込む
        MediaPlayer.Media?.Dispose();
        MediaPlayer.Media = new Media(_libVLC, new Uri(filePath));

        Play();
      }
    }

    private void Play() => MediaPlayer.Play();
    private void Pause() => MediaPlayer.Pause();
    private void Stop() => MediaPlayer.Stop();

    // --- Command CanExecute Conditions ---
    private bool CanPlay() => !IsPlaying && MediaPlayer.Media != null;
    private bool CanPause() => IsPlaying;
    private bool CanStop() => IsPlaying;


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
        // ViewModelが管理するリソースを解放
        MediaPlayer.Stop();
        MediaPlayer.Dispose();
        _libVLC.Dispose();
      }

      _disposed = true;
    }

    #endregion
  }

  #region Helper Classes (MVVM)

  // ... (ViewModelBase, RelayCommand<T>, RelayCommand クラスは変更なし) ...
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

