using Nexo.Core.Assistant;
using Nexo.Core.Documents;

namespace Nexo.Core.Tests;

public sealed class ChartPlanTests
{
    private static AnswerTable Table(string markdown) => Assert.IsType<AnswerTable>(Assert.Single(AnswerMarkdown.Parse(markdown)));

    [Fact]
    public void Years_AreALineChart()
    {
        var chart = ChartPlan.From(Table("| Año | Ventas |\n|---|---|\n| 2022 | 1.200 |\n| 2023 | 1.450 |\n| 2024 | 1.800 |"));

        Assert.NotNull(chart);
        Assert.Equal(ChartKind.Line, chart.Kind);
        Assert.Equal(["2022", "2023", "2024"], chart.Categories);
        Assert.Equal([1200d, 1450d, 1800d], Assert.Single(chart.Series).Values);
        Assert.False(chart.IsPercent);
    }

    [Fact]
    public void PercentagesThatAddUpToAHundred_AreAPie()
    {
        var chart = ChartPlan.From(Table("| Fuente | Parte |\n|---|---|\n| Gas | 60% |\n| Solar | 25% |\n| Eólica | 15% |"));

        Assert.Equal(ChartKind.Pie, chart!.Kind);
        Assert.True(chart.IsPercent);
    }

    [Theory]
    [InlineData("Evolución de las ventas", ChartKind.Line)]
    [InlineData("Distribución por región", ChartKind.Pie)]
    [InlineData("Gráfica de barras por región", ChartKind.Column)]
    public void TheTitle_ChoosesTheKind(string title, ChartKind expected)
    {
        var chart = ChartPlan.From(Table("| Región | Ventas |\n|---|---|\n| Norte | 10 |\n| Centro | 20 |\n| Sur | 15 |"), title);

        Assert.Equal(expected, chart!.Kind);
    }

    [Fact]
    public void FewShortLabels_AreColumns_LongLabels_AreHorizontalBars()
    {
        Assert.Equal(ChartKind.Column, ChartPlan.From(Table("| Equipo | Puntos |\n|---|---|\n| Rojo | 10 |\n| Azul | 12 |"))!.Kind);
        Assert.Equal(ChartKind.Bar, ChartPlan.From(Table("| Región | Unidades |\n|---|---|\n| Norte y noroeste del país | 10 |\n| Centro, Bajío y occidente | 12 |"))!.Kind);
    }

    [Fact]
    public void SeveralNumericColumns_AreSeveralSeries()
    {
        var chart = ChartPlan.From(Table("| Meta | 2030 | 2050 |\n|---|---|---|\n| Limpia | 35% | 50% |\n| Solar | 10% | 20% |"));

        Assert.Equal(["2030", "2050"], chart!.Series.Select(series => series.Name));
        Assert.Equal([1, 2], chart.Series.Select(series => series.Column));
        Assert.Equal(ChartKind.Column, chart.Kind);
    }

    [Theory]
    // Porcentajes y cantidades mezclados.
    [InlineData("| Métrica | Meta | Actual |\n|---|---|---|\n| Alcance | +10 % | 1.250 |\n| Interacción | 2% | 3,5 |")]
    // Una columna de texto.
    [InlineData("| Producto | Precio | Nota |\n|---|---|---|\n| A | 10 | Bueno |\n| B | 12 | Caro |")]
    // Una sola fila.
    [InlineData("| Año | Ventas |\n|---|---|\n| 2024 | 10 |")]
    // Cifras de tamaños que no se comparan en un mismo eje.
    [InlineData("| País | Habitantes | Tasa |\n|---|---|---|\n| A | 1.300.000 | 2 |\n| B | 900.000 | 3 |")]
    public void TablesThatAreNotCharts_StayTables(string markdown) =>
        Assert.Null(ChartPlan.From(Table(markdown)));
}
