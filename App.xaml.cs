using LibVLCSharp.Shared; 
using System;
using System.Configuration;
using System.Data;
using System.Windows;

namespace A23_MVVM
{
  public partial class App : Application
  {
    protected override void OnStartup(StartupEventArgs e)
    {
      base.OnStartup(e);
      try
      {
        Core.Initialize();
      }
      catch (Exception)
      {

      }
    }
  }
}