namespace MatrixMultiplier;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

// IPC: анонимный канал (pipe) между родителем и дочерним процессом-генератором.
// Дочерний процесс пишет в свой stdout, родитель читает его через RedirectStandardOutput.
//
// Двоичный формат потока (порядок байт машины, процессы на одном компьютере):
//   int32  Magic        — защита от мусора в канале
//   int32  n            — размер треугольной A (n x n) и число строк B
//   int32  k            — число столбцов B
//   int64  genMs        — сколько миллисекунд ребёнок генерировал матрицы
//   затем A по строкам (строка i — i + 1 значение int64), затем B по строкам (k значений int64)
// Конец передачи определяется размерами из заголовка: получатель знает, сколько байт ждать.
// В stdout ребёнка нельзя писать ничего лишнего, диагностика — только в stderr.

public record TransferStats(
    double LaunchAndGenerateMs,   // от старта процесса до получения заголовка (запуск + генерация)
    long ChildGenerateMs,         // чистое время генерации, измеренное ребёнком
    double TransferMs,            // приём всех данных после заголовка
    long Bytes);

public record GeneratedData(TriangleMatrix A, Matrix B, TransferStats Stats);

public static class Generator
{
    public const int Magic = 0x4D415452; // "MATR"

    private static int? ParseSeed(string s) =>
        s == "-" ? null : int.Parse(s, CultureInfo.InvariantCulture);

    // Точка входа дочернего процесса
    public static int RunChild(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Использование: generate <n> <k> <seedA|-> <seedB|->");
            return 2;
        }
        int n = int.Parse(args[1], CultureInfo.InvariantCulture);
        int k = int.Parse(args[2], CultureInfo.InvariantCulture);
        int? seedA = ParseSeed(args[3]);
        int? seedB = ParseSeed(args[4]);

        var sw = Stopwatch.StartNew();
        var a = new TriangleMatrix();
        a.GenerateMatrix(n, 0, 10, seedA);
        var b = new Matrix();
        b.GenerateMatrix(n, k, 0, 10, seedB);
        sw.Stop();

        using Stream stdout = Console.OpenStandardOutput();
        using var buffered = new BufferedStream(stdout, 1 << 20);
        using (var w = new BinaryWriter(buffered, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Magic);
            w.Write(n);
            w.Write(k);
            w.Write(sw.ElapsedMilliseconds);
        }
        foreach (long[] row in a.Values!)
            buffered.Write(MemoryMarshal.AsBytes(row.AsSpan()));
        foreach (long[] row in b.Values!)
            buffered.Write(MemoryMarshal.AsBytes(row.AsSpan()));
        buffered.Flush();
        return 0;
    }
}

public static class GeneratorClient
{
    public static GeneratedData Request(int n, int k, int? seedA = null, int? seedB = null)
    {
        string exe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Не удалось определить путь к программе");

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        if (Path.GetFileNameWithoutExtension(exe).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            psi.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        psi.ArgumentList.Add("generate");
        psi.ArgumentList.Add(n.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(k.ToString(CultureInfo.InvariantCulture));
        psi.ArgumentList.Add(seedA?.ToString(CultureInfo.InvariantCulture) ?? "-");
        psi.ArgumentList.Add(seedB?.ToString(CultureInfo.InvariantCulture) ?? "-");

        var total = Stopwatch.StartNew();
        using Process proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Не удалось запустить процесс-генератор");

        Stream pipe = proc.StandardOutput.BaseStream;
        using var reader = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);

        if (reader.ReadInt32() != Generator.Magic)
            throw new InvalidDataException("Неверный заголовок: в канале не данные генератора");
        int rn = reader.ReadInt32();
        int rk = reader.ReadInt32();
        long childGenMs = reader.ReadInt64();
        if (rn != n || rk != k)
            throw new InvalidDataException("Размеры в заголовке не совпадают с запрошенными");
        double waitMs = total.Elapsed.TotalMilliseconds;

        var transfer = Stopwatch.StartNew();
        long bytes = 20; // размер заголовка

        long[][] aRows = new long[n][];
        for (int i = 0; i < n; i++)
        {
            aRows[i] = new long[i + 1];
            pipe.ReadExactly(MemoryMarshal.AsBytes(aRows[i].AsSpan()));
            bytes += (long)(i + 1) * sizeof(long);
        }
        long[][] bRows = new long[n][];
        for (int i = 0; i < n; i++)
        {
            bRows[i] = new long[k];
            pipe.ReadExactly(MemoryMarshal.AsBytes(bRows[i].AsSpan()));
            bytes += (long)k * sizeof(long);
        }
        transfer.Stop();

        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Процесс-генератор завершился с кодом {proc.ExitCode}");

        return new GeneratedData(
            new TriangleMatrix(aRows),
            new Matrix(bRows),
            new TransferStats(waitMs, childGenMs, transfer.Elapsed.TotalMilliseconds, bytes));
    }
}
