using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Linq;
using System.Windows.Controls.Ribbon; 

namespace A23_MVVM
{
  public partial class MainWindow : RibbonWindow 
  {
    public MainWindow()
    {
      InitializeComponent();
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
  }
}