using System.Windows;
using System.Windows.Controls;
using IgnaviorLauncher.ViewModels;

namespace IgnaviorLauncher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void GamesTabList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (DataContext is MainViewModel vm
            && GamesTabList.SelectedItem is GameViewModel game)
        {
            vm.SelectGameCommand.Execute(game);
        }
    }
}