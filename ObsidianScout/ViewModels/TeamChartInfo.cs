using Microcharts;

namespace ObsidianScout.ViewModels;

public class TeamChartInfo
{
    public string TeamNumber { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public Chart Chart { get; set; } = null!;
}
