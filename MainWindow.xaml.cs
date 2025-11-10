using System.Windows;
using System.Windows.Controls;

namespace A23_MVVM
{
  public partial class  MainWindow : Window
  {
    private int _clickCount = 0;

    public MainWindow()
    {
      InitializeComponent();
    }

    private void MyButton_Click(object sender, RoutedEventArgs e)
    {
      _clickCount++;
      InfoTextBlock.Text = $"Clicked {_clickCount} times";
    }
  }
}