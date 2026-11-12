using System.Diagnostics;
using System.Text;
using MelonLoader;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Замер нагрузки мода: сколько миллисекунд за кадр уходит на каждую часть (сеть, вещи, звери...).
    // Session.Tick вызывает Begin, а после каждой части Lap с её номером. Раз в 10 секунд считаем
    // среднее и худший кадр, итог виден в окне мода, а раз в минуту пишется в лог.
    public static class Load
    {
        public const int Net = 0, Puppets = 1, Items = 2, Objects = 3, Extra = 4, Global = 5, Voice = 6, Together = 7,
            Train = 8, Island = 9, Events = 10, Sound = 11, Snapshot = 12, Radial = 13, Weather = 14, Animals = 15,
            Sleep = 16, Progress = 17, Check = 18, Other = 19;

        private static readonly string[] Names =
        {
            "сеть", "фигуры", "вещи", "объекты", "трос/силки/котелки", "общее", "фразы", "вместе", "поезд", "остров",
            "события мира", "звуки", "снимок локации", "палки", "погода", "звери", "сон", "прогресс", "проверка", "прочее",
        };

        private static readonly long[] Acc = new long[Names.Length];
        private static readonly long[] Max = new long[Names.Length];   // самый долгий кусок каждой части за 10 с
        private static long _last, _frameStart, _worst;
        private static int _frames;
        private static float _nextSum, _nextLog;

        // Итог для окна: «0.4 мс/кадр, пик 3.1 мс; больше всего: звери 0.2, вещи 0.1».
        public static string Summary { get; private set; } = "";

        public static void Begin()
        {
            _last = _frameStart = Stopwatch.GetTimestamp();
        }

        public static void Lap(int part)
        {
            long n = Stopwatch.GetTimestamp();
            Acc[part] += n - _last;
            if (n - _last > Max[part]) Max[part] = n - _last;
            _last = n;
        }

        public static void End(float now)
        {
            long frame = Stopwatch.GetTimestamp() - _frameStart;
            if (frame > _worst) _worst = frame;
            _frames++;
            if (now < _nextSum) return;
            _nextSum = now + 10f;
            if (_frames == 0) return;
            double toMs = 1000.0 / Stopwatch.Frequency;
            long total = 0;
            foreach (var a in Acc) total += a;
            var top = new int[3] { -1, -1, -1 };
            for (int i = 0; i < Acc.Length; i++)
                for (int k = 0; k < 3; k++)
                    if (top[k] < 0 || Acc[i] > Acc[top[k]])
                    {
                        for (int j = 2; j > k; j--) top[j] = top[j - 1];
                        top[k] = i;
                        break;
                    }
            var sb = new StringBuilder();
            sb.Append((total * toMs / _frames).ToString("0.00")).Append(UI.L.T(" мс/кадр, пик ", " ms/frame, peak ")).Append((_worst * toMs).ToString("0.0")).Append(UI.L.T(" мс", " ms"));
            bool first = true;
            foreach (int i in top)
            {
                if (i < 0 || Acc[i] * toMs / _frames < 0.01) continue;
                sb.Append(first ? UI.L.T("; больше всего: ", "; most: ") : ", ").Append(Names[i]).Append(' ').Append((Acc[i] * toMs / _frames).ToString("0.00"));
                first = false;
            }
            int mp = 0;
            for (int i = 1; i < Max.Length; i++) if (Max[i] > Max[mp]) mp = i;
            if (Max[mp] * toMs >= 5.0) sb.Append(UI.L.T("; рывок: ", "; spike: ")).Append(Names[mp]).Append(' ').Append((Max[mp] * toMs).ToString("0.0")).Append(UI.L.T(" мс", " ms"));
            Summary = sb.ToString();
            if (now >= _nextLog)
            {
                _nextLog = now + 60f;
                MelonLogger.Msg("[нагрузка] " + Summary);
            }
            for (int i = 0; i < Acc.Length; i++) { Acc[i] = 0; Max[i] = 0; }
            _frames = 0;
            _worst = 0;
        }

        public static void Reset()
        {
            for (int i = 0; i < Acc.Length; i++) Acc[i] = 0;
            _frames = 0;
            _worst = 0;
            Summary = "";
        }
    }
}
