using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace IgnaviorLauncher.ViewModels;

public partial class GameViewModel : ObservableObject
{
    [ObservableProperty]
    private string? name;

    [ObservableProperty]
    private string? installedVersion = "v1.0";

    [ObservableProperty]
    private string? displayVersion;

    [ObservableProperty]
    private string? iconPath;

    [ObservableProperty]
    private string? textState = "Play";

    [ObservableProperty]
    private DateTime? lastPlayed = DateTime.Now;

    public ObservableCollection<PatchNoteViewModel> PatchNotes { get; } = [];

    [ObservableProperty]
    private bool isDownloading;

    [ObservableProperty]
    private double downloadProgress;

    [ObservableProperty]
    private bool isPaused;

    [RelayCommand]
    private void TogglePause()
    {
        IsPaused = !IsPaused;
    }
}