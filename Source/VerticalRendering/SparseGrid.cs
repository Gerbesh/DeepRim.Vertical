using System.Collections.Generic;

namespace DeepRim.Vertical.VerticalRendering;

public sealed class SparseGrid
{
    private readonly Dictionary<long, int> data = new();

    public int this[int x, int y]
    {
        get => data.TryGetValue(Pack(x, y), out var value) ? value : 0;
        set
        {
            var key = Pack(x, y);
            if (value == 0)
            {
                data.Remove(key);
            }
            else
            {
                data[key] = value;
            }
        }
    }

    public void Add(int x, int y, int delta = 1)
    {
        var key = Pack(x, y);
        data.TryGetValue(key, out var value);
        value += delta;
        if (value == 0)
        {
            data.Remove(key);
        }
        else
        {
            data[key] = value;
        }
    }

    public void Subtract(int x, int y, int delta = 1)
    {
        Add(x, y, -delta);
    }

    public IEnumerable<(int x, int y)> ValidSections()
    {
        foreach (var entry in data)
        {
            yield return Unpack(entry.Key);
        }
    }

    private static long Pack(int x, int y)
    {
        return ((long)x << 32) | (uint)y;
    }

    private static (int x, int y) Unpack(long key)
    {
        return ((int)(key >> 32), (int)key);
    }
}
