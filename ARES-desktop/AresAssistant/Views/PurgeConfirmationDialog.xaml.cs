using System.Windows;

namespace AresAssistant.Views;

/// <summary>
/// Diálogo de confirmación para el borrado completo del historial de chat.
/// Requiere una acción explícita del usuario antes de eliminar datos.
/// </summary>
public partial class PurgeConfirmationDialog : Window
{
    public PurgeConfirmationDialog(Window owner)
    {
        InitializeComponent();
        Owner = owner;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
