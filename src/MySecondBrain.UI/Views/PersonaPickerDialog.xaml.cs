using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MySecondBrain.Core.Models;
using MySecondBrain.UI.ViewModels;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace MySecondBrain.UI.Views;

public partial class PersonaPickerDialog : Window
{
    private readonly ChatThreadViewModel _viewModel;

    public PersonaPickerDialog(ChatThreadViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // Wire filter command to search box text changes
        SearchBox.TextChanged += (_, _) => _viewModel.FilterPersonaPickerCommand.Execute(null);

        Loaded += (_, _) =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        };
    }

    private void Window_KeyDown(object sender, WpfKeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
        else if (e.Key == Key.Enter && _viewModel.FilteredPersonaList.Count > 0)
        {
            _viewModel.ActivePersona = _viewModel.FilteredPersonaList[0];
            DialogResult = true;
            Close();
        }
    }

    private void PersonaList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.ActivePersona is not null)
        {
            DialogResult = true;
            Close();
        }
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ActivePersona is not null)
        {
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
