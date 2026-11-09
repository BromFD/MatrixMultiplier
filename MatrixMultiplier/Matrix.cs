using Labs;

namespace MatrixMultiplier;
using System.Linq;
using System.Threading;

public class Matrix
{
    private int? _rows;
    private int? _cols;
    private long[][]? _values;

    public int? Rows
    {
        get { return _rows; }
        private set { _rows = value; }
    }

    public int? Cols
    {
        get { return _cols; }
        private set { _cols = value; }
    }

    public int Length
    {
        get { return (Rows ?? 0) * (Cols ?? 0); }
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
            Rows = value?.Length;
            Cols = value?[0].Length;
        }
    }

    public Matrix(long[][]? values = null)
    {
        Values = values;
    }

    public void GenerateMatrix(int rows, int cols, int min, int max, int? seed = null)
    {
        Random rng = seed == null ? new Random() : new Random(seed.Value);
        long[][] values = Enumerable.Range(0, rows)
            .Select(_ => Enumerable.Range(0, cols)
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
        return Values!
            .Select(row => row[index])
            .ToArray();
    }

    public void UpdateRow(long[] row, int index)
    {
        if (row.Length != Cols) throw new ArgumentException("Не удалось вставить ряд, массив не равен числу столбцов");
        if (index > Rows - 1) throw new ArgumentException("Не удалось вставить ряд, индекс больше числа рядов");
        if (index < 0) throw new ArgumentException("Не удалось вставить ряд, работа с отрицательными индексами не поддерживается");
        Values![index] = row;
    }

    public void UpdateCol(long[] col, int index)
    {
        if (col.Length != Rows) throw new ArgumentException("Не удалось вставить столбец, массив не равен числу рядов");
        if (index > Cols - 1) throw new ArgumentException("Не удалось вставить столбец, индекс больше числа рядов");
        if (index < 0) throw new ArgumentException("Не удалось вставить столбец, работа с отрицательными индексами не поддерживается");
        for (int i = 0; i < Values!.Length; i++) 
            Values[i][index] = col[i];
    }
    
    public Matrix Transpose()
    {
        int rows = Rows!.Value, cols = Cols!.Value;
        long[][] buffer = new long[cols][];
        for (int j = 0; j < cols; j++) buffer[j] = new long[rows];
        for (int i = 0; i < rows; i++)
        {
            long[] row = Values![i];
            for (int j = 0; j < cols; j++) buffer[j][i] = row[j];
        }
        return new Matrix(buffer);
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