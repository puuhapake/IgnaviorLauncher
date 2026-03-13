using CommunityToolkit.Mvvm.ComponentModel;
namespace IgnaviorLauncher.ViewModels;

public partial class PatchNoteViewModel : ObservableObject
{
    [ObservableProperty]
    private string version;

    [ObservableProperty]
    private string markdownContent;

    [ObservableProperty]
    private DateTime releaseDate;
}