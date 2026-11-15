using System.Buffers.Binary;
using Xunit;

namespace RusEfiTools.Gui.Tests;

public sealed class SensorLogDataTests
{
    [Fact]
    public void CanSeekAcrossCompressedBlocksAndReplaceDuplicateTimes()
    {
        var data = new SensorLogData([new("Sensor", "unit", 4, 0, 2, 1)], 4);
        byte[] payload = new byte[4];
        for (int index = 0; index < 300; index++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(payload, (uint)index);
            data.AddSample(index, payload);
            if (index == 127)
            {
                BinaryPrimitives.WriteUInt32BigEndian(payload, 500);
                data.AddSample(index, payload);
            }
        }
        data.Seal();
        Assert.Equal(300, data.Count);
        Assert.Equal(600, data.ValueAt(299, 0));
        Assert.Equal(1002, data.ValueAt(127, 0));
        Assert.Equal(2, data.ValueAt(0, 0));
        Assert.Equal(258, data.ValueAt(128, 0));
        Assert.Equal(0, data.FindNearest(-10));
        Assert.Equal(299, data.FindNearest(999));
        Assert.Equal(1, data.FindNearest(1.5f));
        Assert.Equal(2, data.FindNearest(1.6f));
    }

    [Fact]
    public void InspectorInterpolatesAtCursorTimeAndHoldsFlags()
    {
        var data = new SensorLogData([
            new("RPM", "rpm", 2, 0, 1, 0),
            new("Flag", "", 0, 2, 1, 0, 4)
        ], 3);
        data.AddSample(0, [0x03, 0xE8, 0]);
        data.AddSample(2, [0x0B, 0xB8, 1]);
        data.Seal();
        Assert.Equal(new double[] { 1500, 0 }, data.ValuesAt(0.5f));
        Assert.Equal(new double[] { 2500, 0 }, data.ValuesAt(1.5f));
        Assert.Equal(new double[] { 3000, 1 }, data.ValuesAt(2));
    }

    [Fact]
    public void BitfieldsAreDisplayedAsRawValuesWithoutScalarTransform()
    {
        var field = new MlgLogReader.Field("Flags", "", 10, 0, float.NaN, float.NaN);
        Assert.Equal(5, field.Read([5]));
    }
}
