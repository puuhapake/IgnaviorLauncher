using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IgnaviorLauncher.Views;
using ViewModels;

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

            flowDoc.FontFamily = new("/Assets/Fonts/static/Inter_18pt-Light#Inter Light");
            flowDoc.Foreground = Brushes.White;
            flowDoc.TextAlignment = TextAlignment.Left;

            MarkdownViewer.Document = flowDoc;
        }
    }
}