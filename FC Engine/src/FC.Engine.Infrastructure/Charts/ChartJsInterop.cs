using FC.Engine.Domain.Models;
using Microsoft.JSInterop;

namespace FC.Engine.Infrastructure.Charts;

public class ChartJsInterop
{
    private readonly IJSRuntime _js;

    public ChartJsInterop(IJSRuntime js)
    {
        _js = js;
    }

    public Task RenderLineChart(string canvasId, TrendData data)
    {
        return _js.InvokeVoidAsync("renderChart", canvasId, "line", data).AsTask();
    }

    public Task RenderBarChart(string canvasId, TrendData data)
    {
        return _js.InvokeVoidAsync("renderChart", canvasId, "bar", data).AsTask();
    }

    public Task RenderDoughnutChart(string canvasId, TrendData data)
    {
        return _js.InvokeVoidAsync("renderChart", canvasId, "doughnut", data).AsTask();
    }
}
