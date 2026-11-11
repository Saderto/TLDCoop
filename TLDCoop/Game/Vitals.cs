using System;
using Il2Cpp;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Здоровье и беды игрока для подписи под ником: полоска состояния и слова «замерзает», «кровь»…
    // Своё считаем раз в секунду и отправляем вместе с позицией (Session.SendOwnState).
    public static class Vitals
    {
        public const byte Freezing = 1, Bleeding = 2, Hungry = 4, Thirsty = 8, Exhausted = 16;

        private static byte _hp = 255, _flags;
        private static float _next;

        public static void Own(out byte hp, out byte flags)
        {
            float now = Time.unscaledTime;
            if (now >= _next)
            {
                _next = now + 1f;
                Measure();
            }
            // Здоровье под ником выключено (своей галочкой или хостом для всех) — твою полоску и беды не видят и другие.
            hp = Session.Sees(Shown.Status) ? _hp : (byte)255;
            flags = Session.Sees(Shown.Status) ? _flags : (byte)0;
        }

        private static void Measure()
        {
            _hp = 255;
            _flags = 0;
            try
            {
                var cond = GameManager.GetConditionComponent();
                if (cond != null) _hp = (byte)Mathf.Clamp(Mathf.RoundToInt(cond.GetNormalizedCondition() * 100f), 0, 100);
                var fr = GameManager.GetFreezingComponent();
                if (fr != null && fr.IsFreezing()) _flags |= Freezing;
                var bl = GameManager.GetBloodLossComponent();
                if (bl != null && bl.HasBloodLoss()) _flags |= Bleeding;
                var hu = GameManager.GetHungerComponent();
                if (hu != null)
                {
                    float max = hu.GetAdjustedMaxReserveCalories();
                    if (hu.IsStarving() || (max > 0f && hu.GetCalorieReserves() / max < 0.2f)) _flags |= Hungry;
                }
                var th = GameManager.GetThirstComponent();
                if (th != null && (th.IsDehydrated() || th.IsVeryThirsty())) _flags |= Thirsty;
                var fa = GameManager.GetFatigueComponent();
                if (fa != null && fa.IsExhausted()) _flags |= Exhausted;
            }
            catch { }
        }

        // Слова под полоской, по одному на беду, своим цветом.
        public static string Words(byte flags)
        {
            if (flags == 0) return "";
            var sb = new System.Text.StringBuilder();
            void Add(byte f, string color, string text)
            {
                if ((flags & f) == 0) return;
                if (sb.Length > 0) sb.Append("  ");
                sb.Append("<color=").Append(color).Append('>').Append(text).Append("</color>");
            }
            Add(Freezing, "#8fd3ff", "замерзает");
            Add(Bleeding, "#ff5a4a", "кровь");
            Add(Hungry, "#f0b860", "голод");
            Add(Thirsty, "#7fb0ff", "жажда");
            Add(Exhausted, "#c0a0ff", "без сил");
            return sb.ToString();
        }

        public static Color BarColor(int hp) =>
            hp > 60 ? new Color(0.45f, 0.8f, 0.4f) : hp > 30 ? new Color(0.95f, 0.75f, 0.3f) : new Color(0.95f, 0.3f, 0.25f);
    }
}
