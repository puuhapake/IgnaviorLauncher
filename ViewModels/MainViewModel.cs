using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace IgnaviorLauncher.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<GameViewModel> games;

    private GameViewModel selectedGame;

    public GameViewModel SelectedGame
    {
        get => selectedGame;
        set
        {
            if (SetProperty(ref selectedGame, value) && value != null)
            {
                SelectGameCommand.Execute(value);
            }
        }
    }

    public MainViewModel()
    {
        LoadDummyData(); // test only!
    }

    private void LoadDummyData()
    {
        var games = new ObservableCollection<GameViewModel>();

        var game1 = new GameViewModel
        {
            Name = "Alpha Game",
            InstalledVersion = "v1.2.3",
            TextState = "Play"
        };
        game1.PatchNotes.Add(new PatchNoteViewModel
        {
            Version = "1.2.3",
            ReleaseDate = new DateTime(2026, 3, 14),
            MarkdownContent = @"## New Features
- Added support for new control system
- Improved rendering performance

### Bugs
- Fixed bugs"
        });
        game1.PatchNotes.Add(new PatchNoteViewModel
        {
            Version = "1.2.2",
            ReleaseDate = new DateTime(2026, 3, 13),
            MarkdownContent = @"- Nothing interesting"
        });

        var game2 = new GameViewModel
        {
            Name = "Beta Game",
            InstalledVersion = "",
            TextState = "Install"
        };
        game2.PatchNotes.Add(new PatchNoteViewModel
        {
            Version = "v0.9",
            ReleaseDate = new DateTime(2023, 9, 2),
            MarkdownContent = @"## New Features
- Added new level, _The Backrooms_
- Beta release"
        });

        games.Add(game1);
        games.Add(game2);

        Games = [.. games.OrderByDescending(g => g.LastPlayed)];
        SelectedGame = Games.FirstOrDefault();
    }

    [RelayCommand]
    private void SelectGame(GameViewModel game)
    {
        if (game == null)
            return;

        // currently LastClicked, not LastPlayed!
        game.LastPlayed = DateTime.Now;

        var reordered = Games.OrderByDescending(g => g.LastPlayed).ToList();
        Games.Clear();
        foreach (var g in reordered)
        {
            Games.Add(g);
        }

        SelectedGame = Games.FirstOrDefault(g => g == game);
    }
}