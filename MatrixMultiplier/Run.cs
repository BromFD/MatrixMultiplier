namespace MatrixMultiplier;

using System.Diagnostics;
using System.Globalization;
using Labs;

public class Run
{
    private static string Ms(double ms) => ms.ToString("F3", CultureInfo.InvariantCulture);

    private static void WarnIfTooManyThreads(int threads)
    {
        int pc = Environment.ProcessorCount;
        if (threads > pc)
            Console.WriteLine($"Предупреждение! Ваш компьютер имеет всего {pc} логических процессоров, " +
                              $"это меньше заявленных {threads} потоков. " +
                              "Больше потоков не приведёт к более быстрой работе программы.");
    }

    // Потоки: 1..pc с шагом 1, затем 1.5x, 2x, 4x, 8x от числа логических процессоров
    private static int[] ThreadCounts()
    {
        int pc = Environment.ProcessorCount;
        return Enumerable.Range(1, pc)
            .Concat(new[] { pc * 3 / 2, pc * 2, pc * 4, pc * 8 })
            .Distinct().OrderBy(x => x).ToArray();
    }

    // Задание на «3»: обычные матрицы

    public static void Test()
    {
        Matrix matrix1 = new Matrix();
        Matrix matrix2 = new Matrix();
        MatrixMultiplier mm = new MatrixMultiplier();
        int rows1 = Scanner.GenericInput<int>("Введите число строк первой матрицы: ", "Неверно!", Scanner.IsNatural);
        int cols1 = Scanner.GenericInput<int>("Введите число столбцов первой матрицы: ", "Неверно!", Scanner.IsNatural);
        int rows2 = Scanner.GenericInput<int>("Введите число строк второй матрицы: ", "Неверно!", Scanner.IsNatural);
        int cols2 = Scanner.GenericInput<int>("Введите число столбцов второй матрицы: ", "Неверно!", Scanner.IsNatural);
        if (cols1 != rows2)
        {
            Console.WriteLine("Число столбцов первой матрицы должно равняться числу строк второй.");
            return;
        }
        int? seed1 = Scanner.NullableInput<int>("Введите seed для первой матрицы (Enter — случайный): ", "Применён стандартный seed...", Scanner.IsNatural);
        int? seed2 = Scanner.NullableInput<int>("Введите seed для второй матрицы (Enter — случайный): ", "Применён стандартный seed...", Scanner.IsNatural);
        int threads = Scanner.GenericInput<int>("Введите число потоков: ", "Неверно!", Scanner.IsNatural);
        WarnIfTooManyThreads(threads);
        matrix1.GenerateMatrix(rows1, cols1, 0, 10, seed1);
        matrix2.GenerateMatrix(rows2, cols2, 0, 10, seed2);
        Console.WriteLine("Выполняются вычисления...");
        Stopwatch stopwatch = Stopwatch.StartNew();
        Matrix result = mm.Multiply(matrix1, matrix2, threads);
        stopwatch.Stop();
        Console.WriteLine($"Контрольная сумма: {result.Sum()}");
        Console.WriteLine($"Матрица посчиталась за {Ms(stopwatch.Elapsed.TotalMilliseconds)} мс");
    }

    // Задание на «4»: треугольная A, генератор в отдельном процессе

    public static void TestTriangle()
    {
        MatrixMultiplier mm = new MatrixMultiplier();
        int n = Scanner.GenericInput<int>("Размер n (A — нижнетреугольная n x n): ", "Неверно!", Scanner.IsNatural);
        int k = Scanner.GenericInput<int>("Число столбцов k матрицы B (n x k): ", "Неверно!", Scanner.IsNatural);
        int? seed1 = Scanner.NullableInput<int>("Введите seed для A (Enter — случайный): ", "Применён стандартный seed...", Scanner.IsNatural);
        int? seed2 = Scanner.NullableInput<int>("Введите seed для B (Enter — случайный): ", "Применён стандартный seed...", Scanner.IsNatural);
        int threads = Scanner.GenericInput<int>("Введите число потоков: ", "Неверно!", Scanner.IsNatural);
        int mode = Scanner.GenericInput<int>("Распределение (1 — статическое, 2 — динамическое): ", "Неверно!", x => x is 1 or 2);
        int chunk = 1;
        if (mode == 2)
            chunk = Scanner.GenericInput<int>("Размер порции строк (например, 1, 8, 64): ", "Неверно!", Scanner.IsNatural);
        Distribution dist = mode == 1 ? Distribution.Static : Distribution.Dynamic;
        WarnIfTooManyThreads(threads);

        Console.WriteLine("Запускается процесс-генератор...");
        GeneratedData data = GeneratorClient.Request(n, k, seed1, seed2);
        TransferStats s = data.Stats;
        Console.WriteLine($"Запуск + генерация: {Ms(s.LaunchAndGenerateMs)} мс (из них генерация в ребёнке {s.ChildGenerateMs} мс)");
        Console.WriteLine($"Передача по каналу: {Ms(s.TransferMs)} мс, {s.Bytes / 1024.0 / 1024.0:F1} МБ");

        Console.WriteLine("Выполняются вычисления...");
        Stopwatch stopwatch = Stopwatch.StartNew();
        Matrix result = mm.Multiply(data.A, data.B, threads, dist, chunk);
        stopwatch.Stop();
        Console.WriteLine($"Контрольная сумма: {result.Sum()}");
        Console.WriteLine($"Умножение: {Ms(stopwatch.Elapsed.TotalMilliseconds)} мс");
    }

    // Проверка корректности на малых данных 

    private static long[][] Naive(long[][] a, long[][] b)
    {
        int cols = b[0].Length;
        var c = new long[a.Length][];
        for (int i = 0; i < a.Length; i++)
        {
            c[i] = new long[cols];
            for (int j = 0; j < cols; j++)
            {
                long sum = 0;
                for (int t = 0; t < a[i].Length; t++) sum += a[i][t] * b[t][j];
                c[i][j] = sum;
            }
        }
        return c;
    }

    private static bool Same(long[][] x, long[][] y) =>
        x.Length == y.Length && x.Zip(y).All(p => p.First.SequenceEqual(p.Second));

    private static bool Check(string name, long[][] actual, long[][] expected)
    {
        bool ok = Same(actual, expected);
        Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {name}");
        return ok;
    }

    public static void SelfCheck()
    {
        var mm = new MatrixMultiplier();
        bool ok = true;

        var a = new Matrix(); a.GenerateMatrix(13, 9, 0, 10, 1);
        var b = new Matrix(); b.GenerateMatrix(9, 11, 0, 10, 2);
        long[][] expected = Naive(a.Values!, b.Values!);
        foreach (int t in new[] { 1, 2, 3, 7, 100 })
            ok &= Check($"обычные матрицы, T={t}",
                mm.Multiply(a, b, t, Distribution.Static, 1, false).Values!, expected);

        var tri = new TriangleMatrix(); tri.GenerateMatrix(37, 0, 10, 3);
        var b2 = new Matrix(); b2.GenerateMatrix(37, 23, 0, 10, 4);
        expected = Naive(tri.Values!, b2.Values!);
        var variants = new (Distribution, int)[]
        {
            (Distribution.Static, 1), (Distribution.Dynamic, 1),
            (Distribution.Dynamic, 5), (Distribution.Dynamic, 64)
        };
        foreach (int t in new[] { 1, 3, 8, 100 })
            foreach (var (dist, chunk) in variants)
                ok &= Check($"треугольная, T={t}, {dist}, порция {chunk}",
                    mm.Multiply(tri, b2, t, dist, chunk, false).Values!, expected);

        Console.WriteLine(ok ? "Все проверки пройдены." : "ЕСТЬ ОШИБКИ!");
    }

    // Сбор статистики 

    public static void CollectStatsDense(string path = "results_dense.csv")
    {
        int[] sizes = { 500, 1500, 3000 };
        int[] threadCounts = ThreadCounts();
        MatrixMultiplier mm = new MatrixMultiplier();

        // Прогрев JIT
        var w = new Matrix(); w.GenerateMatrix(500, 500, 0, 10);
        mm.Multiply(w, w, Environment.ProcessorCount, Distribution.Static, 1, false);

        using var csv = new StreamWriter(path);
        csv.AutoFlush = true;
        csv.WriteLine("size;threads;run;ms;sum");

        for (int i = 0; i < sizes.Length; i++)
        {
            var a = new Matrix(); a.GenerateMatrix(sizes[i], sizes[i], 0, 10, i);
            var b = new Matrix(); b.GenerateMatrix(sizes[i], sizes[i], 0, 10, i + 1);
            foreach (int t in threadCounts)
            {
                Console.WriteLine($"[dense] размер {sizes[i]}, потоков {t}");
                for (int run = 1; run <= 5; run++)
                {
                    var sw = Stopwatch.StartNew();
                    Matrix result = mm.Multiply(a, b, t, Distribution.Static, 1, false);
                    sw.Stop();
                    csv.WriteLine($"{sizes[i]};{t};{run};{Ms(sw.Elapsed.TotalMilliseconds)};{result.Sum()}");
                }
            }
        }
        Console.WriteLine($"Готово: {path}");
    }

    public static void CollectStatsTriangle(string path = "results_triangle.csv")
    {
        int[] sizes = { 500, 1500, 3800 };
        int[] threadCounts = ThreadCounts();
        var variants = new (Distribution, int)[]
        {
            (Distribution.Static, 0), (Distribution.Dynamic, 1)
        };
        MatrixMultiplier mm = new MatrixMultiplier();

        var w = new TriangleMatrix(); w.GenerateMatrix(500, 0, 10);
        var wb = new Matrix(); wb.GenerateMatrix(500, 500, 0, 10);
        mm.Multiply(w, wb, Environment.ProcessorCount, Distribution.Dynamic, 1, false);

        using var csv = new StreamWriter(path);
        csv.AutoFlush = true;
        csv.WriteLine("size;threads;mode;chunk;run;ms;sum");

        for (int i = 0; i < sizes.Length; i++)
        {
            var a = new TriangleMatrix(); a.GenerateMatrix(sizes[i], 0, 10, i);
            var b = new Matrix(); b.GenerateMatrix(sizes[i], sizes[i], 0, 10, i + 1);
            long expectedSum = -1;
            foreach (int t in threadCounts)
            {
                Console.WriteLine($"[triangle] размер {sizes[i]}, потоков {t}");
                foreach (var (dist, chunk) in variants)
                    for (int run = 1; run <= 5; run++)
                    {
                        var sw = Stopwatch.StartNew();
                        Matrix result = mm.Multiply(a, b, t, dist, Math.Max(chunk, 1), false);
                        sw.Stop();
                        long sum = result.Sum();
                        if (expectedSum < 0) expectedSum = sum;
                        else if (sum != expectedSum)
                            Console.WriteLine($"!!! Расхождение контрольной суммы: {sum} вместо {expectedSum} " +
                                              $"(размер {sizes[i]}, T={t}, {dist})");
                        csv.WriteLine($"{sizes[i]};{t};{dist};{chunk};{run};{Ms(sw.Elapsed.TotalMilliseconds)};{sum}");
                    }
            }
        }
        Console.WriteLine($"Готово: {path}");
    }

    // Время запуска + генерации и время передачи данных через канал
    public static void CollectStatsTransfer(string path = "results_transfer.csv")
    {
        int[] sizes = { 500, 1500, 3800 };
        using var csv = new StreamWriter(path);
        csv.AutoFlush = true;
        csv.WriteLine("size;run;launch_and_generate_ms;child_generate_ms;transfer_ms;bytes");

        GeneratorClient.Request(100, 100, 1, 2); // прогрев
        foreach (int n in sizes)
        {
            Console.WriteLine($"[transfer] размер {n}");
            for (int run = 1; run <= 5; run++)
            {
                TransferStats s = GeneratorClient.Request(n, n, n, n + 1).Stats;
                csv.WriteLine($"{n};{run};{Ms(s.LaunchAndGenerateMs)};{s.ChildGenerateMs};{Ms(s.TransferMs)};{s.Bytes}");
            }
        }
        Console.WriteLine($"Готово: {path}");
    }
}
