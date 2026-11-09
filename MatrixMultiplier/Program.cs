using Labs;

namespace MatrixMultiplier;

class Program
{
    static int Main(string[] args)
    {
        // Режим дочернего процесса-генератора
        if (args.Length > 0 && args[0] == "generate")
            return Generator.RunChild(args);

        // Запуск без меню: <exe> stats | stats4 | transfer | check  [путь к csv]
        if (args.Length > 0)
        {
            string? path = args.Length > 1 ? args[1] : null;
            switch (args[0])
            {
                case "stats":
                    if (path == null) Run.CollectStatsDense(); else Run.CollectStatsDense(path);
                    return 0;
                case "stats4":
                    if (path == null) Run.CollectStatsTriangle(); else Run.CollectStatsTriangle(path);
                    return 0;
                case "transfer":
                    if (path == null) Run.CollectStatsTransfer(); else Run.CollectStatsTransfer(path);
                    return 0;
                case "check":
                    Run.SelfCheck();
                    return 0;
                default:
                    Console.WriteLine("Неизвестная команда. Доступно: stats, stats4, transfer, check");
                    return 1;
            }
        }

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("1 — умножение обычных матриц (оценка 3)");
            Console.WriteLine("2 — треугольная матрица, генератор в отдельном процессе (оценка 4)");
            Console.WriteLine("3 — проверка корректности на малых данных");
            Console.WriteLine("4 — сбор статистики: обычные матрицы");
            Console.WriteLine("5 — сбор статистики: треугольная матрица, static vs dynamic");
            Console.WriteLine("6 — сбор статистики: передача данных между процессами");
            Console.WriteLine("0 — выход");
            int choice = Scanner.GenericInput<int>("Выбор: ", "Неверно!", x => x is >= 0 and <= 6);
            switch (choice)
            {
                case 1: Run.Test(); break;
                case 2: Run.TestTriangle(); break;
                case 3: Run.SelfCheck(); break;
                case 4: Run.CollectStatsDense(); break;
                case 5: Run.CollectStatsTriangle(); break;
                case 6: Run.CollectStatsTransfer(); break;
                default: return 0;
            }
        }
    }
}
