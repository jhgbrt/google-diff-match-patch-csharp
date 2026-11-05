using System.Buffers;

namespace DiffMatchPatch;

/// <summary>
/// A growable list of <see cref="DiffRange"/> backed by <see cref="ArrayPool{T}"/>.
/// The backing array is returned to the pool on <see cref="Dispose"/>.
/// </summary>
internal sealed class RangeList : IDisposable
{
    private DiffRange[] _items = Array.Empty<DiffRange>();
    private int _count;

    public int Count => _count;

    public DiffRange this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            return _items[index];
        }
        set
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            _items[index] = value;
        }
    }

    public ReadOnlySpan<DiffRange> AsSpan() => _items.AsSpan(0, _count);

    public ReadOnlySpan<DiffRange> AsSpan(int start) => _items.AsSpan(start, _count - start);

    public void Add(DiffRange item)
    {
        if (_count == _items.Length) Grow(_count + 1);
        _items[_count++] = item;
    }

    public void AddRange(ReadOnlySpan<DiffRange> items)
    {
        EnsureCapacity(_count + items.Length);
        items.CopyTo(_items.AsSpan(_count));
        _count += items.Length;
    }

    public void Insert(int index, DiffRange item)
    {
        if ((uint)index > (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_count == _items.Length) Grow(_count + 1);
        Array.Copy(_items, index, _items, index + 1, _count - index);
        _items[index] = item;
        _count++;
    }

    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        _count--;
        Array.Copy(_items, index + 1, _items, index, _count - index);
    }

    /// <summary>Remove all items from <paramref name="index"/> on.</summary>
    public void Truncate(int index)
    {
        if ((uint)index > (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        _count = index;
    }

    public void Clear() => _count = 0;

    public void EnsureCapacity(int capacity)
    {
        if (capacity > _items.Length) Grow(capacity);
    }

    private void Grow(int minimum)
    {
        var capacity = Math.Max(minimum, Math.Max(16, _items.Length * 2));
        var items = ArrayPool<DiffRange>.Shared.Rent(capacity);
        Array.Copy(_items, items, _count);
        ReturnArray();
        _items = items;
    }

    private void ReturnArray()
    {
        if (_items.Length > 0)
            ArrayPool<DiffRange>.Shared.Return(_items);
    }

    public void Dispose()
    {
        ReturnArray();
        _items = Array.Empty<DiffRange>();
        _count = 0;
    }
}
