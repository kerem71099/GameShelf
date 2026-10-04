using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
