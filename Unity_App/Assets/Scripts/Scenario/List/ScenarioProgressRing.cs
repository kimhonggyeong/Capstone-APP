using UnityEngine;
using XCharts.Runtime;

public class ScenarioProgressRing : MonoBehaviour
{
    [SerializeField] private RingChart ringChart;

    public void SetProgress(float percent, string caption)
    {
        float value = Mathf.Clamp(percent, 0f, 100f);

        if (ringChart == null)
            return;

        Ring serie = GetOrCreateRing(caption);
        LabelStyle label = ConfigureCenterLabel(serie);
        label.formatter = "{b}";

        serie.ClearData();
        string labelText = value.ToString("0") + "%";
        ringChart.AddData(serie.index, value, 100, labelText);
        ringChart.RefreshChart();
    }

    public void SetReturn(float returnPct)
    {
        if (ringChart == null)
            return;

        float ringValue = Mathf.Clamp(Mathf.Abs(returnPct), 0f, 100f);
        Ring serie = GetOrCreateRing("수익률");
        LabelStyle label = ConfigureCenterLabel(serie);
        label.formatter = "{b}";

        serie.ClearData();
        string labelText = FormatSigned(returnPct) + "%";
        ringChart.AddData(serie.index, ringValue, 100, labelText);
        ringChart.RefreshChart();
    }

    private Ring GetOrCreateRing(string serieName)
    {
        Ring serie = ringChart.GetSerie(0) as Ring;
        if (serie == null)
            serie = ringChart.AddSerie<Ring>(serieName);

        return serie;
    }

    private static LabelStyle ConfigureCenterLabel(Ring serie)
    {
        LabelStyle label = serie.EnsureComponent<LabelStyle>();
        label.show = true;
        label.position = LabelStyle.Position.Center;
        return label;
    }

    private static string FormatSigned(float value)
    {
        return (value > 0f ? "+" : "") + value.ToString("0.##");
    }
}
