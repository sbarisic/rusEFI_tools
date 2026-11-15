using System.IO.Compression;

namespace RusEfiTools.Gui;

internal sealed class SensorLogData
{
    private const int SamplesPerBlock = 128;
    private readonly int recordSize;
    private readonly List<byte[]> blocks = [];
    private readonly List<float> times = [];
    private byte[] buffer;
    private int bufferedSamples;
    private int cachedBlock = -1;
    private bool sealedData;
    private (int Index, double[] Values)? firstSnapshot;
    private (int Index, double[] Values)? secondSnapshot;

    public IReadOnlyList<MlgLogReader.Field> Fields { get; }
    public int Count => times.Count;

    public SensorLogData(MlgLogReader.Field[] fields, int recordSize)
    {
        Fields = fields;
        this.recordSize = recordSize;
        buffer = new byte[recordSize * SamplesPerBlock];
    }

    public void AddSample(float time, ReadOnlySpan<byte> payload)
    {
        if (sealedData)
        {
            throw new InvalidOperationException("Sensor data is already sealed.");
        }
        if (times.Count > 0 && time == times[^1])
        {
            payload.CopyTo(buffer.AsSpan((bufferedSamples - 1) * recordSize, recordSize));
            return;
        }
        if (bufferedSamples == SamplesPerBlock)
        {
            Flush();
        }
        payload.CopyTo(buffer.AsSpan(bufferedSamples * recordSize, recordSize));
        bufferedSamples++;
        times.Add(time);
    }

    public void Seal()
    {
        if (!sealedData)
        {
            Flush();
            sealedData = true;
        }
    }

    private void Flush()
    {
        if (bufferedSamples == 0)
        {
            return;
        }
        using var storage = new MemoryStream();
        using (var compressor = new DeflateStream(storage, CompressionLevel.Fastest, leaveOpen: true))
        {
            compressor.Write(buffer.AsSpan(0, bufferedSamples * recordSize));
        }
        blocks.Add(storage.ToArray());
        bufferedSamples = 0;
    }

    public int FindNearest(float time)
    {
        if (times.Count == 0)
        {
            return -1;
        }
        int index = times.BinarySearch(time);
        if (index >= 0)
        {
            return index;
        }
        index = ~index;
        if (index == 0)
        {
            return 0;
        }
        if (index == times.Count)
        {
            return times.Count - 1;
        }
        return time - times[index - 1] <= times[index] - time ? index - 1 : index;
    }

    public float TimeAt(int sample) => times[sample];

    public double[] ValuesAt(float time)
    {
        int index = times.BinarySearch(time);
        if (index >= 0)
        {
            return Snapshot(index);
        }
        int right = ~index;
        if (right == 0 || right == Count)
        {
            return Snapshot(Math.Clamp(right, 0, Count - 1));
        }

        var before = Snapshot(right - 1);
        var after = Snapshot(right);
        double amount = (time - times[right - 1]) / (times[right] - times[right - 1]);
        var values = new double[Fields.Count];
        for (int field = 0; field < values.Length; field++)
        {
            // Interpolate numeric signals; boolean, enum and bit-field states
            // remain at the last recorded state until the next sample.
            values[field] = Fields[field].Type >= 10 || Fields[field].DisplayStyle != 0
                ? before[field]
                : before[field] + amount * (after[field] - before[field]);
        }
        return values;
    }

    private double[] Snapshot(int sample)
    {
        if (firstSnapshot is { } first && first.Index == sample)
        {
            return first.Values;
        }
        if (secondSnapshot is { } second && second.Index == sample)
        {
            return second.Values;
        }
        var values = new double[Fields.Count];
        for (int field = 0; field < values.Length; field++)
        {
            values[field] = ValueAt(sample, field);
        }
        firstSnapshot = secondSnapshot;
        secondSnapshot = (sample, values);
        return values;
    }

    public double ValueAt(int sample, int field)
    {
        if (!sealedData || sample < 0 || sample >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(sample));
        }
        int block = sample / SamplesPerBlock;
        if (cachedBlock != block)
        {
            using var storage = new MemoryStream(blocks[block], writable: false);
            using var decompressor = new DeflateStream(storage, CompressionMode.Decompress);
            int records = Math.Min(SamplesPerBlock, Count - block * SamplesPerBlock);
            decompressor.ReadExactly(buffer.AsSpan(0, records * recordSize));
            cachedBlock = block;
        }
        return Fields[field].Read(buffer.AsSpan(sample % SamplesPerBlock * recordSize, recordSize));
    }
}
