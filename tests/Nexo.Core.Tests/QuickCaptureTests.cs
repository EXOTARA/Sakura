using Nexo.Core.Tasks;
using Xunit;

namespace Nexo.Core.Tests;

public sealed class QuickCaptureTests
{
    // Miércoles 16 de septiembre de 2026, 15:00.
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 15, 0, 0, TimeSpan.FromHours(-6));

    private static DateTimeOffset At(int month, int day, int hour = 0, int minute = 0) =>
        new(2026, month, day, hour, minute, 0, Now.Offset);

    [Theory]
    [InlineData("entregar U2A2 el viernes a las 5", "Entregar U2A2", 9, 18, 17, 0)]
    [InlineData("llamar al dentista mañana a las 10", "Llamar al dentista", 9, 17, 10, 0)]
    [InlineData("pagar la luz 20/09", "Pagar la luz", 9, 20, 0, 0)]
    [InlineData("examen el 3 de octubre 18:30", "Examen", 10, 3, 18, 30)]
    [InlineData("junta a las 9 am", "Junta", 9, 17, 9, 0)]
    [InlineData("cena a las 8 pm", "Cena", 9, 16, 20, 0)]
    [InlineData("revisar correo hoy", "Revisar correo", 9, 16, 0, 0)]
    [InlineData("comprar regalo pasado mañana", "Comprar regalo", 9, 18, 0, 0)]
    public void UnderstandsTheDateAsItIsSaid(string text, string title, int month, int day, int hour, int minute)
    {
        var result = QuickCapture.Parse(text, Now);

        Assert.Equal(title, result.Title);
        Assert.Equal(At(month, day, hour, minute), result.DueAt);
    }

    [Fact]
    public void SameWeekday_MeansNextWeek() =>
        Assert.Equal(At(9, 23), QuickCapture.Parse("gimnasio el miércoles", Now).DueAt);

    [Fact]
    public void APastDate_IsNextYear() =>
        Assert.Equal(new DateTimeOffset(2027, 1, 3, 0, 0, 0, Now.Offset), QuickCapture.Parse("renovar 3 de enero", Now).DueAt);

    [Fact]
    public void ANumberThatIsNotAnHour_StaysInTheTitle()
    {
        var result = QuickCapture.Parse("leer capítulo 3", Now);

        Assert.Equal("Leer capítulo 3", result.Title);
        Assert.Null(result.DueAt);
    }

    [Fact]
    public void InTheMorning_IsNotTomorrow()
    {
        var result = QuickCapture.Parse("correr por la mañana", Now);

        Assert.Equal("Correr por la mañana", result.Title);
        Assert.Null(result.DueAt);
    }

    [Theory]
    [InlineData("estudiar !")]
    [InlineData("importante: estudiar")]
    public void MarksImportant(string text)
    {
        var result = QuickCapture.Parse(text, Now);

        Assert.Equal(TaskPriority.High, result.Priority);
        Assert.Equal("Estudiar", result.Title);
    }

    [Fact]
    public void WithoutADate_UsesTheDayBeingViewed() =>
        Assert.Equal(At(9, 25), QuickCapture.Parse("comprar pan", Now, new DateOnly(2026, 9, 25)).DueAt);

    [Fact]
    public void AnHourAlreadyGone_IsForTomorrow() =>
        Assert.Equal(At(9, 17, 9, 0), QuickCapture.Parse("llamar 9:00", Now).DueAt);
}
