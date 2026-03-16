using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IgnaviorLauncher.ViewModels;

namespace IgnaviorLauncher.Views;

public partial class PatchNoteView : UserControl
{
    public PatchNoteView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is PatchNoteViewModel note)
        {
            var markdown = new Markdown.Xaml.Markdown();
            var flowDoc = markdown.Transform(note.MarkdownContent);
            flowDoc.FontFamily = new("/Assets/Fonts/Inter-VariableFont_opsz,wght.ttf#Inter");
            flowDoc.Foreground = Brushes.White;
            flowDoc.FontWeight = FontWeights.Light;
            MarkdownViewer.Document = flowDoc;
        }
    }
}