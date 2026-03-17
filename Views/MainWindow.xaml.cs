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
        SizeChanged += OnSizeChanged;
    }

    private void GamesTabList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (DataContext is MainViewModel vm
            && GamesTabList.SelectedItem is GameViewModel game)
        {
            vm.SelectGameCommand.Execute(game);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            double scale = e.NewSize.Width / 1060;
            scale = Math.Max(1.0, Math.Min(1.2, scale));
            vm.ChangelogScale = scale;
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = true;
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        SettingsPopup.IsOpen = false;
    }
}