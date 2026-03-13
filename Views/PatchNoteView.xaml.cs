using System.Windows.Controls;
using IgnaviorLauncher.ViewModels;

namespace IgnaviorLauncher.Views;

public partial class PatchNoteView : UserControl
{
    public PatchNoteView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is PatchNoteViewModel note)
        {
            var markdown = new Markdown.Xaml.Markdown();
            var flowDoc = markdown.Transform(note.MarkdownContent);
            MarkdownViewer.Document = flowDoc;
        }
    }
}