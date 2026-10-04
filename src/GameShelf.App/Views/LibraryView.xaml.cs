using System.Windows.Controls;

namespace GameShelf.App.Views;

public partial class LibraryView : System.Windows.Controls.UserControl
{
    public LibraryView()
    {
        InitializeComponent();
    }

    /// <summary>Ctrl+F kısayolu için arama kutusuna odaklanır.</summary>
    public void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}
