using System.Collections;

namespace DesertBus;

public interface IPooled
{
    bool IsDisposed { get; }
    void Reset();
}

public class Pool<T> : IEnumerable<T> where T : IPooled
{
    private readonly List<T> Values;
    private readonly Func<T> Create;

    public Pool(int size, Func<T> create)
    {
        this.Values = new List<T>(size);
        this.Create = create;
    }

    public T Get()
    {
        // Find disposed
        foreach (T value in this.Values)
        {
            if (value.IsDisposed)
            {
                value.Reset();
                return value;
            }
        }
        // Add new
        if (this.Values.Count < this.Values.Capacity)
        {
            T value = this.Create();
            this.Values.Add(value);
            value.Reset();
            return value;
        }
        // Get oldest
        {
            T value = this.Values.First();
            value.Reset();
            return value;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return this.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }
}
