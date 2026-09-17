using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Nexo.Core.Assistant;

namespace Nexo.App;

/// <summary>Lo que se decidió en el panel del Borrador de tarea.</summary>
public sealed record AssignmentDraftChoice(string Prompt, AssignmentDetails? RememberedDetails, bool AskedPersonal);

/// <summary>2026-09-16 — confirmación del Borrador de tarea (fases 4 y 5).</summary>
public partial class AssignmentDraftWindow : Window
{
    private readonly TextBox _subject;
    private readonly TextBox _teacher;
    private readonly TextBox _activity;
    private readonly TextBox _name;
    private readonly TextBox _studentId;
    private readonly TextBox _program;
    private readonly TextBox _school;
    private AssignmentKind _kind;

    /// <param name="taskText">Lo que la persona compartió; decide el tipo sugerido y si parece examen.</param>
    /// <param name="saved">Los datos personales recordados, si los hay.</param>
    /// <param name="personalAlreadyAsked">Si ya se preguntaron y se prefirió no darlos: no se insiste.</param>
    public AssignmentDraftWindow(string taskText, AssignmentKind? chosen, AssignmentDetails? saved, bool personalAlreadyAsked)
    {
        InitializeComponent();

        _subject = Field(ActivityFields, "Asignatura", new Thickness(0, 0, 0, 6));
        _teacher = Field(ActivityFields, "Profesor", new Thickness(0, 0, 0, 6));
        _activity = Field(ActivityFields, "Actividad", new Thickness(0, 0, 0, 6));
        _name = Field(PersonalFields, "Nombre", new Thickness(0, 0, 4, 6), saved?.Name);
        _studentId = Field(PersonalFields, "Matrícula", new Thickness(4, 0, 0, 6), saved?.StudentId);
        _program = Field(PersonalFields, "Carrera", new Thickness(0, 0, 4, 6), saved?.Program);
        _school = Field(PersonalFields, "Universidad", new Thickness(4, 0, 0, 6), saved?.School);
        RememberCheckBox.IsChecked = saved is not null;

        // Se pregunta una vez; si se prefirió no darlos, quedan como huecos y el formulario no insiste.
        if (personalAlreadyAsked && saved is null)
        {
            PersonalPanel.Visibility = Visibility.Collapsed;
            ShowPersonalButton.Visibility = Visibility.Visible;
        }

        _kind = chosen ?? AssignmentDraft.Suggest(taskText);
        BuildKinds(AssignmentDraft.Suggest(taskText));

        if (AssignmentDraft.LooksLikeExam(taskText))
        {
            ExamNotice.Visibility = Visibility.Visible;
            FormPanel.Visibility = Visibility.Collapsed;
        }

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    /// <summary>El resultado, si se pidió algo; null si se canceló.</summary>
    public AssignmentDraftChoice? Choice { get; private set; }

    private static TextBox Field(Panel parent, string label, Thickness margin, string? value = null)
    {
        var box = new TextBox { Text = value ?? string.Empty };
        box.SetResourceReference(StyleProperty, "CompactTextBoxStyle");
        System.Windows.Automation.AutomationProperties.SetName(box, label);

        var hint = new TextBlock
        {
            Text = label,
            Margin = new Thickness(13, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed
        };
        hint.SetResourceReference(StyleProperty, "MutedTextStyle");
        box.TextChanged += (_, _) => hint.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        var cell = new Grid { Margin = margin, Children = { box, hint } };
        parent.Children.Add(cell);
        return box;
    }

    private void BuildKinds(AssignmentKind suggested)
    {
        KindsPanel.Children.Clear();
        foreach (var kind in AssignmentDraft.All.OrderByDescending(kind => kind == suggested))
        {
            var selected = kind == _kind;
            var button = new Button
            {
                Content = (selected ? "✓ " : string.Empty) + AssignmentDraft.Label(kind) + (kind == suggested ? " · sugerido" : string.Empty),
                Tag = kind,
                Margin = new Thickness(0, 0, 6, 6),
                ToolTip = string.Join(" · ", AssignmentDraft.Sections(kind)),
                FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal
            };
            button.SetResourceReference(StyleProperty, "SoftChipButtonStyle");
            button.Click += (_, _) =>
            {
                _kind = kind;
                BuildKinds(suggested);
            };
            KindsPanel.Children.Add(button);
        }
    }

    private void Draft_Click(object sender, RoutedEventArgs e)
    {
        var details = new AssignmentDetails(
            _name.Text, _studentId.Text, _program.Text, _school.Text,
            _subject.Text, _teacher.Text, _activity.Text);
        var personalShown = PersonalPanel.Visibility == Visibility.Visible;
        var remembered = personalShown && RememberCheckBox.IsChecked == true
            ? new AssignmentDetails(_name.Text.Trim(), _studentId.Text.Trim(), _program.Text.Trim(), _school.Text.Trim())
            : null;

        Choice = new AssignmentDraftChoice(AssignmentDraft.BuildPrompt(_kind, details), remembered, personalShown);
        Close();
    }

    private void Tutor_Click(object sender, RoutedEventArgs e)
    {
        Choice = new AssignmentDraftChoice(AssignmentDraft.BuildTutorPrompt(), null, AskedPersonal: false);
        Close();
    }

    private void NotAnExam_Click(object sender, RoutedEventArgs e)
    {
        ExamNotice.Visibility = Visibility.Collapsed;
        FormPanel.Visibility = Visibility.Visible;
    }

    private void ShowPersonal_Click(object sender, RoutedEventArgs e)
    {
        PersonalPanel.Visibility = Visibility.Visible;
        ShowPersonalButton.Visibility = Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
