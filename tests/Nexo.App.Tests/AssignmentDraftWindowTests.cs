using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Nexo.Core.Assistant;

namespace Nexo.App.Tests;

[Collection(StaWpfCollection.Name)]
public sealed class AssignmentDraftWindowTests(StaWpfFixture wpf)
{
    private static Button ButtonWithContent(DependencyObject root, string content) =>
        Descendants<Button>(root).First(button => button.Content as string == content);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var logical = LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>();
        foreach (var child in logical)
        {
            if (child is T typed)
            {
                yield return typed;
            }

            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    [Fact]
    public void AnExam_OffersTheTutor_NotTheDraft()
    {
        wpf.Invoke(() =>
        {
            var window = new AssignmentDraftWindow("Examen parcial · Pregunta 2 de 8 · Tiempo restante 10:00", null, null, false);
            try
            {
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("ExamNotice")!).Visibility);
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("FormPanel")!).Visibility);

                ButtonWithContent(window, "Explícame cómo se resuelve").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.Equal(AssignmentDraft.BuildTutorPrompt(), window.Choice?.Prompt);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ADraft_CarriesTheRememberedDetails_OnlyWhenAsked()
    {
        wpf.Invoke(() =>
        {
            var saved = new AssignmentDetails("Ana", "A01", "Ingeniería", "UADY");
            var window = new AssignmentDraftWindow("Resuelve los ejercicios del inciso a)", null, saved, true);
            try
            {
                Assert.True(((CheckBox)window.FindName("RememberCheckBox")!).IsChecked);
                ButtonWithContent(window, "Hacer el borrador").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.Contains("Nombre: Ana", window.Choice!.Prompt);
                Assert.Contains("desarrollo por inciso", window.Choice.Prompt, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("Ana", window.Choice.RememberedDetails?.Name);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void AfterDecliningOnce_ThePersonalDataIsNotAskedAgain()
    {
        wpf.Invoke(() =>
        {
            var window = new AssignmentDraftWindow("Ensayo sobre ética", null, null, personalAlreadyAsked: true);
            try
            {
                Assert.Equal(Visibility.Collapsed, ((FrameworkElement)window.FindName("PersonalPanel")!).Visibility);
                Assert.Equal(Visibility.Visible, ((FrameworkElement)window.FindName("ShowPersonalButton")!).Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
