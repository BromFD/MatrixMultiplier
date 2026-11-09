namespace MatrixMultiplier;

public enum Distribution { Static, Dynamic }

public class MatrixMultiplier
{
    // Прогресс считается в "единицах работы": для обычной матрицы строка весит 1,
    // для треугольной строка i весит i + 1 (столько умножений на элемент результата).
    private long _workDone;
    private long _workTotal;

    public int Progress =>
        _workTotal == 0 ? 100 : (int)(Interlocked.Read(ref _workDone) * 100 / _workTotal);

    private static long Dot(long[] row, long[] col, int length)
    {
        long sum = 0;
        for (int i = 0; i < length; i++)
            sum += row[i] * col[i];
        return sum;
    }

    // Обычная матрица A (n x m) на B (m x k)
    public Matrix Multiply(
        Matrix a,
        Matrix b,
        int threads,
        Distribution distribution = Distribution.Static,
        int chunk = 1,
        bool showProgress = true)
    {
        if (a.Cols != b.Rows)
            throw new ArgumentException("Не удалось умножить строку на столбец, несовпадение длин");
        return MultiplyRows(a.Values!, b, threads, distribution, chunk, showProgress, _ => 1);
    }

    // Нижнетреугольная A (n x n, строка i хранит i + 1 элемент) на B (n x k)
    public Matrix Multiply(
        TriangleMatrix a,
        Matrix b,
        int threads,
        Distribution distribution = Distribution.Static,
        int chunk = 1,
        bool showProgress = true)
    {
        if (a.Size != b.Rows)
            throw new ArgumentException("Размер треугольной матрицы должен равняться числу строк второй матрицы");
        return MultiplyRows(a.Values!, b, threads, distribution, chunk, showProgress, i => i + 1);
    }

    // Общее ядро: строка aRows[i] может быть короче, чем число строк B (треугольный случай),
    // тогда суммируются только первые aRows[i].Length слагаемых (остальные элементы A равны нулю).
    private Matrix MultiplyRows(
        long[][] aRows,
        Matrix b,
        int threads,
        Distribution distribution,
        int chunk,
        bool showProgress,
        Func<int, long> weight)
    {
        if (threads < 1) throw new ArgumentOutOfRangeException(nameof(threads));
        if (chunk < 1) throw new ArgumentOutOfRangeException(nameof(chunk));

        int rows = aRows.Length;
        int cols = b.Cols!.Value;

        // Транспонирование идёт в одном потоке и входит в замер времени (разрешено заданием,
        // но его нужно упомянуть в отчёте): так строки B лежат подряд в памяти.
        long[][] bt = b.Transpose().Values!;

        long[][] result = new long[rows][];
        for (int i = 0; i < rows; i++) result[i] = new long[cols];

        _workDone = 0;
        _workTotal = 0;
        for (int i = 0; i < rows; i++) _workTotal += weight(i);

        void ComputeRow(int i)
        {
            long[] ai = aRows[i];
            long[] ci = result[i];
            for (int j = 0; j < cols; j++)
                ci[j] = Dot(ai, bt[j], ai.Length);
            Interlocked.Add(ref _workDone, weight(i));
        }

        Thread[] workers = distribution == Distribution.Static
            ? CreateStaticWorkers(rows, threads, ComputeRow)
            : CreateDynamicWorkers(rows, threads, chunk, ComputeRow);

        foreach (var thread in workers) thread.Start();
        if (showProgress) ReportProgress(workers);
        else foreach (var thread in workers) thread.Join();

        return new Matrix(result);
    }

    // Статическое распределение: T непрерывных блоков с (почти) одинаковым числом строк.
    // Остаток rows % threads раздаётся по одной лишней строке первым потокам.
    private static Thread[] CreateStaticWorkers(int rows, int threads, Action<int> computeRow)
    {
        int perThread = rows / threads;
        int extra = rows % threads;
        Thread[] workers = new Thread[threads];
        int start = 0;
        for (int t = 0; t < threads; t++)
        {
            int from = start;
            int to = start + perThread + (t < extra ? 1 : 0);
            start = to;
            workers[t] = new Thread(() =>
            {
                for (int i = from; i < to; i++) computeRow(i);
            });
        }
        return workers;
    }

    // Динамическое распределение: общий счётчик next, каждый поток атомарно
    // забирает следующую порцию из chunk строк, пока строки не закончатся.
    private static Thread[] CreateDynamicWorkers(int rows, int threads, int chunk, Action<int> computeRow)
    {
        int next = 0;
        Thread[] workers = new Thread[threads];
        for (int t = 0; t < threads; t++)
        {
            workers[t] = new Thread(() =>
            {
                while (true)
                {
                    int from = Interlocked.Add(ref next, chunk) - chunk;
                    if (from >= rows) break;
                    int to = Math.Min(from + chunk, rows);
                    for (int i = from; i < to; i++) computeRow(i);
                }
            });
        }
        return workers;
    }

    public void ReportProgress(Thread[] workers)
    {
        int last = -1;
        while (workers.Any(t => t.IsAlive))
        {
            int p = Progress;
            if (p != last)
            {
                Console.Write($"\rПрогресс: {p}%");
                last = p;
            }
            Thread.Sleep(50);
        }
        foreach (var t in workers) t.Join();
        Console.WriteLine("\rПрогресс: 100%");
    }
}
