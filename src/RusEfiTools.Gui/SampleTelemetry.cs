using FishUI;
using FishUI.Controls;

namespace RusEfiTools.Gui;

internal sealed record TelemetryChannel(LineChartSeries Series, float AxisMaximum, bool RightAxis);

internal static class SampleTelemetry
{
    public static TelemetryChannel[] Create()
    {
        var map = new LineChartSeries("MAP (kPa)", new FishColor(65, 145, 255));
        var rpm = new LineChartSeries("RPM (rpm)", new FishColor(255, 78, 78));
        var tps = new LineChartSeries("TPS (%)", new FishColor(100, 235, 55));
        var random = new Random(42);

        for (int index = 0; index <= 400; index++)
        {
            float time = index / 10f;
            float throttle = 100 * Ramp(time, 10.5f, 12) * (1 - Ramp(time, 30, 31.3f));
            float pressure = 40 + 70 * Ramp(time, 11, 17) * (1 - Ramp(time, 30, 33));
            float speed = Interpolate(time,
                [0, 5, 10, 15, 20, 23, 27, 30, 33, 37, 40],
                [1100, 1800, 2900, 4500, 6000, 6650, 6300, 5300, 3500, 2800, 2500]);

            map.AddPoint(time, pressure + (float)random.NextDouble() * 1.5f - 0.75f);
            rpm.AddPoint(time, speed + (float)random.NextDouble() * 70 - 35);
            tps.AddPoint(time, throttle);
        }

        return [new(map, 200, false), new(rpm, 8000, true), new(tps, 200, false)];
    }

    private static float Ramp(float time, float start, float end)
    {
        float amount = Math.Clamp((time - start) / (end - start), 0, 1);
        return amount * amount * (3 - 2 * amount);
    }

    private static float Interpolate(float time, float[] times, float[] values)
    {
        for (int index = 1; index < times.Length; index++)
        {
            if (time <= times[index])
            {
                float amount = (time - times[index - 1]) / (times[index] - times[index - 1]);
                return values[index - 1] + amount * (values[index] - values[index - 1]);
            }
        }

        return values[^1];
    }
}
