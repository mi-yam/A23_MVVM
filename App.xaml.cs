using System.Configuration;
using System.Data;
using System.Windows;
using LibVLCSharp.Shared; // ★ 1. using を追加

// 名前空間は "A23_MVVM"
namespace A23_MVVM
{
  public partial class App : Application
  {
    // ★ 2. OnStartup メソッドをオーバーライド (override)
    protected override void OnStartup(StartupEventArgs e)
    {
      base.OnStartup(e);

      // ★ 3. ViewModelが作られる「前」に、VLCコアを初期化
      // これにより、ネイティブDLLが最初にロードされます。
      Core.Initialize();
    }
  }
}