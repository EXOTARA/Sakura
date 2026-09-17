using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nexo.Core.Productization;

namespace Nexo.App;

/// <summary>2026-09-16 — «Enviar comentarios».</summary>
public partial class FeedbackWindow : Window
{
    private readonly string _version;

    public FeedbackWindow(string version)
    {
        _version = version;
        InitializeComponent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
        Loaded += (_, _) => FeedbackTextBox.Focus();
    }

    private string? Version => IncludeVersionsCheckBox.IsChecked == true ? _version : null;

    private string? Windows => IncludeVersionsCheckBox.IsChecked == true ? RuntimeInformation.OSDescription : null;

    private void FeedbackTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var canSend = FeedbackReport.CanSend(FeedbackTextBox.Text);
        CopyButton.IsEnabled = canSend;
        OpenButton.IsEnabled = canSend;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(FeedbackReport.Body(FeedbackTextBox.Text, Version, Windows));
            StatusText.Text = "Copiado. Pégalo en un mensaje o correo para quien te pasó Sakura.";
        }
        catch (ExternalException)
        {
            StatusText.Text = "El portapapeles está ocupado; inténtalo otra vez.";
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(FeedbackReport.IssueUrl(FeedbackTextBox.Text, Version, Windows)) { UseShellExecute = true });
            Close();
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            StatusText.Text = "No pude abrir el navegador. Usa «Copiar».";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
