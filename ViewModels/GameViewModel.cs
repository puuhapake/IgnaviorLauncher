using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace IgnaviorLauncher.ViewModels;

public partial class GameViewModel : ObservableObject
{
    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private string installedVersion = "v1.0.0"; // placeholder

    [ObservableProperty]
    private string displayVersion;

    [ObservableProperty]
    private string iconPath;

    [ObservableProperty]
    private string textState = "Play";

    [ObservableProperty]
    private DateTime lastPlayed = DateTime.Now;

    public ObservableCollection<PatchNoteViewModel> PatchNotes { get; } = [];
}