using Nexo.Core.Assistant;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class AssignmentDraftTests
{
    [Theory]
    [InlineData("Resuelve los siguientes ejercicios con el método de bisección, inciso a) y b)", AssignmentKind.Exercises)]
    [InlineData("Elabora un ensayo argumentativo sobre tu postura ante la IA", AssignmentKind.Essay)]
    [InlineData("Reporte de práctica de laboratorio: péndulo simple", AssignmentKind.Report)]
    [InlineData("Investiga y haz un resumen de la Revolución Industrial", AssignmentKind.Research)]
    [InlineData("Realiza un mapa conceptual de los tipos de energía", AssignmentKind.Presentation)]
    [InlineData("Trae tu credencial el lunes", AssignmentKind.Generic)]
    [InlineData("", AssignmentKind.Generic)]
    public void Suggest_ReadsTheKindOfWork(string task, AssignmentKind expected) =>
        Assert.Equal(expected, AssignmentDraft.Suggest(task));

    [Fact]
    public void Exercises_FollowTheAgreedSections()
    {
        Assert.Equal(
            ["Portada", "Objetivos", "Introducción", "Planteamiento del problema", "Marco teórico",
             "Desarrollo por inciso", "Resultados", "Conclusiones", "Referencias", "Anexos"],
            AssignmentDraft.Sections(AssignmentKind.Exercises));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryPrompt_ScaffoldsInsteadOfSolving_AndNeverInventsPeopleOrSources(AssignmentKind kind)
    {
        var prompt = AssignmentDraft.BuildPrompt(kind);

        Assert.Contains("no el trabajo resuelto", prompt);
        Assert.Contains("[Nombre]", prompt);
        Assert.Contains("No inventes referencias", prompt);
        Assert.All(AssignmentDraft.Sections(kind), section => Assert.Contains(section, prompt));
    }

    [Fact]
    public void ExercisesPrompt_AsksForEquationsAndGeoGebraPlaceholders()
    {
        var prompt = AssignmentDraft.BuildPrompt(AssignmentKind.Exercises);

        Assert.Contains("$$", prompt);
        Assert.Contains("[Gráfica de GeoGebra:", prompt);
    }

    public static TheoryData<AssignmentKind> Kinds()
    {
        var data = new TheoryData<AssignmentKind>();
        foreach (var kind in AssignmentDraft.All)
        {
            data.Add(kind);
        }

        return data;
    }
}
