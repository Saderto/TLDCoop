using System;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace TLDCoop.Game
{
    // У друга иногда пропадают постройки (рыбацкий барак на льду Загадочного озера: мебель стоит, дома нет).
    // Много неподвижных моделей игра рисует не сама по себе, а пачками через RenderObjectInstance: их рендереры
    // выключены, а модели рисуются из списка. Если такой список сломался, модели не видно совсем.
    // Здесь: что загружено и в каком состоянии эти списки (в лог после входа в мир и после зова хоста,
    // и в render.txt в «Собрать логи»). Кнопку «Перерисовать мир» (выключить и включить списки) убрал: игра
    // после этого не собирает списки заново (моделей 0) — пропадает весь лес и дома, лечится только загрузкой.
    public static class RenderCheck
    {
        private static float _logAt = -1f;
        private static string _logWhy = "";

        // Записать сводку через несколько секунд (после телепорта/загрузки игра ещё раскладывает сцену).
        public static void LogSoon(string why, float delay = 5f)
        {
            _logAt = Time.unscaledTime + delay;
            _logWhy = why;
        }

        private static string _scene = "";

        public static void Tick(float now)
        {
            // Новая локация (у хоста и у друга): через 10 с записать, что загрузилось — чтобы сравнить логи.
            if (GameApi.InGame())
            {
                string sc = GameApi.Scene();
                if (sc != _scene) { _scene = sc; LogSoon("локация " + sc, 10f); }
            }
            else _scene = "";
            if (_logAt > 0f && now > _logAt)
            {
                _logAt = -1f;
                if (!GameApi.InGame()) return;
                MelonLogger.Msg($"[отрисовка] {_logWhy}: {Summary()}");
                MelonLogger.Msg("[отрисовка] сцены: " + Scenes());
            }
        }

        public static string Scenes()
        {
            var sb = new StringBuilder();
            try
            {
                for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                {
                    var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(s.name);
                    if (!s.isLoaded) sb.Append(" (не загружена)");
                }
            }
            catch (Exception e) { sb.Append("! " + e.Message); }
            return sb.ToString();
        }

        // Коротко: сколько списков, сколько из них включено и готово, сколько моделей в них и сколько видно.
        public static string Summary()
        {
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<RenderObjectInstance>(true);
                int on = 0, init = 0, off = 0, objects = 0, visible = 0, batches = 0;
                var bad = new List<string>();
                foreach (var r in all)
                {
                    if (r == null) continue;
                    bool active = r.isActiveAndEnabled;
                    bool ok = false;
                    try { ok = r.m_IsInitialized; } catch { }
                    if (active) on++; else off++;
                    if (ok) init++;
                    try { objects += r.m_ObjectCount; } catch { }
                    try { var b = r.m_Batches; if (b != null) { visible += b.m_DebugVisibleCount; batches += b.m_DebugRenderedBatchCount; } } catch { }
                    if (active && !ok && bad.Count < 5) bad.Add(Name(r.gameObject));
                }
                string s = $"списков {all.Length}, включено {on}, выключено {off}, готово {init}, моделей {objects}, видно {visible}, пачек {batches}";
                if (bad.Count > 0) s += "; включены, но не готовы: " + string.Join(", ", bad);
                return s;
            }
            catch (Exception e) { return "! " + e.Message; }
        }

        // Подробно для render.txt: каждый список и что рядом с игроком.
        public static string Describe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Сцены: " + Scenes());
            sb.AppendLine("Списки: " + Summary());
            try
            {
                foreach (var r in UnityEngine.Object.FindObjectsOfType<RenderObjectInstance>(true))
                {
                    if (r == null) continue;
                    string cam = "?", st = "";
                    try { cam = r.m_Camera != null ? r.m_Camera.name : "нет"; } catch { }
                    try { st = $"готов {r.m_IsInitialized}, выделено {r.m_Allocated}, был выключен {r.m_HasBeenDisabled}, моделей {r.m_ObjectCount}, вид {r.m_Category}"; } catch { }
                    try { var b = r.m_Batches; if (b != null) st += $", видно {b.m_DebugVisibleCount}/{b.m_DebugObjectCount}, пачек {b.m_DebugRenderedBatchCount}"; } catch { }
                    sb.AppendLine($"  {Name(r.gameObject)} [{r.gameObject.scene.name}] вкл {r.isActiveAndEnabled}, камера {cam}, {st}");
                }
            }
            catch (Exception e) { sb.AppendLine("  ! " + e.Message); }
            try
            {
                var pt = GameManager.GetPlayerTransform();
                if (pt != null)
                {
                    var me = pt.position;
                    sb.AppendLine($"Игрок: {me.x:0.0} {me.y:0.0} {me.z:0.0}");
                    var near = new List<(float d, string s)>();
                    foreach (var mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>(true))
                    {
                        if (mr == null) continue;
                        float d = Vector3.Distance(mr.bounds.center, me);
                        if (d > 30f) continue;
                        near.Add((d, $"{d,5:0.0} м  {Name(mr.gameObject)} [{mr.gameObject.scene.name}] актив {mr.gameObject.activeInHierarchy}, рендер {mr.enabled}, виден {mr.isVisible}"));
                    }
                    near.Sort((a, b) => a.d.CompareTo(b.d));
                    sb.AppendLine($"Модели в 30 м: {near.Count}");
                    for (int i = 0; i < near.Count && i < 150; i++) sb.AppendLine("  " + near[i].s);
                }
            }
            catch (Exception e) { sb.AppendLine("  ! " + e.Message); }
            return sb.ToString();
        }

        private static string Name(GameObject go)
        {
            if (go == null) return "?";
            var t = go.transform;
            string s = t.name;
            if (t.parent != null) s = t.parent.name + "/" + s;
            if (t.parent != null && t.parent.parent != null) s = t.parent.parent.name + "/" + s;
            return s;
        }
    }
}
