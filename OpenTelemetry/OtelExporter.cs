using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TUnitOtel.OpenTelemetry;

internal class OtelExporter : IDisposable
{
    private readonly TracerProvider _tracerProvider;
    private readonly MeterProvider _meterProvider;

    public OtelExporter()
    {
        var serviceName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? "TUnit";
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName);

        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddSource("*")
            .AddOtlpExporter()
            .SetSampler(new AlwaysOnSampler())
            .Build();

        _meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter("*")
            .AddOtlpExporter()
            .Build();
    }

    public void Dispose()
    {
        _tracerProvider.ForceFlush();
        _meterProvider.ForceFlush();
        _tracerProvider.Dispose();
        _meterProvider.Dispose();
    }
}
