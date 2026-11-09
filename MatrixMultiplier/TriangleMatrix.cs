namespace MatrixMultiplier;
using Labs;

public class TriangleMatrix
{
    private int? _size;
    private long[][]? _values;

    public int? Size
    {
        get { return _size; }
        private set { _size = value; }
    }
    public int Length
    {
        get { return (Size * (Size + 1) / 2 ?? 0); }
    }

    public long[][]? Values
    {
        get
        {
            if (_values == null) throw new InvalidOperationException("Матрица не инициализирована");
            return _values;
        }
        private set
        {
            _values = value;
            Size = value?.Length;
        }
    }

    public TriangleMatrix(long[][]? values = null)
    {
        Values = values;
    }
    
    public void GenerateMatrix(int size, int min, int max, int? seed = null)
    {
        Random rng = seed == null ? new Random() : new Random(seed.Value);
        long[][] values = Enumerable.Range(0, size)
            .Select(i => Enumerable.Range(0, i + 1)
                .Select(_ => rng.NextInt64(min, max)
                ).ToArray())
            .ToArray();
        Values = values;
    }
    
    public long[]? Row(int index)
    {
        return Values![index];
    }
    
    public long[]? Col(int index)
    {
        long[] col = new long[(int)Size! - index];
        int colIndex = 0;
        for (int i = index; i < Size; i++)
        {
            col[colIndex] = Values![i][index];
            colIndex++;
        }
        return col;
    }
    
    public TriangleMatrix Transpose()
    {
        long[][] buffer = new long[Size!.Value][];
        for (int i = 0; i < buffer.Length; i++) buffer[i] = Col(i)!;
        return new TriangleMatrix(buffer);
    }

    public long Sum()
    {
        long sum = 0;
        foreach (var row in Values ?? [])
        {
            foreach (var element in row)
            {
                sum += element;
            }
        }
        return sum;
    }
    
    public override string ToString()
    {
        string values = "";
        Array.ForEach(Values!, row => 
            values += ArrayHelper
                .ArrayToString(row, "\t") + "\n");
        return values;
    }
}