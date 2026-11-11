using System;
using System.Globalization;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace TLDCoop.Game
{
    // Где лежит спящий (0.19.51). Игра не двигает игрока на кровать: он спит там, где стоял, и фигура у остальных
    // ложилась от его ног в сторону взгляда — наискосок через кровать, рядом со спальником или в стене.
    // Теперь спящий в кровати или в разложенном спальнике (Rest.m_Bed) шлёт в строке занятости (Game/Busy.cs)
    // «bed:x;y;z;yaw»: середину тела на матрасе и куда голова. Фигура у остальных ложится туда (Game/RemotePlayer.cs).
    //  • Длинная сторона — по размерам сетки и коллайдеров кровати в её собственных осях.
    //  • Высота матраса — лучом сверху вниз от 1,1 м над полом (верхний ярус двухъярусной выше и не мешает).
    //  • Голова — к ближней стене по длинной стороне (кровать обычно стоит изголовьем к стене); стен нет —
    //    в ту сторону, куда игрок смотрел.
    // Спальник в рюкзаке (не разложен) — как раньше, у ног игрока.
    internal static class BedPose
    {
        private static IntPtr _bed;
        private static string _key = "";

        internal static string Key()
        {
            var rest = GameManager.m_Rest;
            if (rest == null || !rest.IsSleeping()) { _bed = IntPtr.Zero; _key = ""; return ""; }
            var bed = rest.m_Bed;
            if (bed == null) return "";
            if (bed.Pointer == _bed) return _key;
            _bed = bed.Pointer;
            try { _key = Compute(bed); }
            catch (Exception e) { _key = ""; MelonLogger.Warning("[сон] где лежу: " + e.Message); }
            return _key;
        }

        private static string Compute(Bed bed)
        {
            var gi = bed.GetComponent<GearItem>();
            if (gi != null && (gi.m_InPlayerInventory || gi.m_InsideContainer))
            {
                MelonLogger.Msg("[сон] сплю в спальнике из рюкзака — фигура лежит у моих ног");
                return "";
            }
            var me = GameManager.GetPlayerTransform();
            if (me == null) return "";
            Transform frame = bed.transform;
            Transform geo = frame;
            if (gi != null && bed.m_BedRollPlacedMesh != null && bed.m_BedRollPlacedMesh.activeInHierarchy) geo = bed.m_BedRollPlacedMesh.transform;
            if (!LocalBounds(frame, geo, out var lb) && frame.parent != null)
            {
                frame = frame.parent;
                if (!LocalBounds(frame, frame, out lb)) lb = default;
            }
            Vector3 size = Vector3.Scale(lb.size, frame.lossyScale);
            float lx = Mathf.Abs(size.x), lz = Mathf.Abs(size.z);
            float len = Mathf.Max(lx, lz), wid = Mathf.Min(lx, lz);
            string place = $"{bed.name} ({(gi != null ? "спальник" : "кровать")}, {len:0.00}×{wid:0.00} м)";
            if (len < 1.2f || len > 3.2f || wid < 0.35f || wid > 2.4f)
            {
                MelonLogger.Msg($"[сон] {place}: размеры не похожи на кровать — фигура лежит у моих ног");
                return "";
            }
            var axis = frame.TransformDirection(lz >= lx ? Vector3.forward : Vector3.right);
            axis.y = 0f;
            if (axis.sqrMagnitude < 1e-4f) return "";
            axis.Normalize();
            var mid = frame.TransformPoint(lb.center);
            float floor = me.position.y;
            float top = Surface(mid, floor);
            mid.y = top;

            // Голова: ближняя стена по длинной стороне (свои коллайдеры кровати не в счёт).
            float fwd = Wall(mid, axis, bed.transform), back = Wall(mid, -axis, bed.transform);
            string why;
            float sign;
            if (fwd < 9f || back < 9f)
            {
                if (Mathf.Abs(fwd - back) > 0.3f) { sign = fwd < back ? 1f : -1f; why = "изголовьем к стене"; }
                else { sign = 0f; why = ""; }
            }
            else { sign = 0f; why = ""; }
            if (sign == 0f)
            {
                var look = GameManager.GetMainCamera() != null ? GameManager.GetMainCamera().transform.forward : me.forward;
                look.y = 0f;
                sign = Vector3.Dot(look, axis) >= 0f ? 1f : -1f;
                why = "головой туда, куда смотрел";
            }
            var head = axis * sign;
            float yaw = Mathf.Atan2(head.x, head.z) * Mathf.Rad2Deg;
            string bp = bed.m_BodyPlacementTransform != null
                ? $"; точка тела игры {Fmt(bed.m_BodyPlacementTransform.position)} поворот {bed.m_BodyPlacementTransform.eulerAngles.y:0}°"
                : "";
            MelonLogger.Msg($"[сон] лёг: {place}, матрас на {top - floor:0.00} м над полом, {why} (стены {(fwd < 9f ? fwd.ToString("0.0", CultureInfo.InvariantCulture) : "—")}/{(back < 9f ? back.ToString("0.0", CultureInfo.InvariantCulture) : "—")} м){bp}");
            return "bed:" + string.Format(CultureInfo.InvariantCulture, "{0:0.00};{1:0.00};{2:0.00};{3:0}", mid.x, mid.y, mid.z, yaw);
        }

        // Размеры сеток и коробок-коллайдеров geo в осях frame.
        private static bool LocalBounds(Transform frame, Transform geo, out Bounds b)
        {
            b = default;
            bool any = false;
            var inv = frame.worldToLocalMatrix;
            foreach (var mf in geo.GetComponentsInChildren<MeshFilter>())
            {
                if (mf == null || mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
                Add(ref b, ref any, inv * mf.transform.localToWorldMatrix, mf.sharedMesh.bounds);
            }
            foreach (var bc in geo.GetComponentsInChildren<BoxCollider>())
            {
                if (bc == null || !bc.enabled || !bc.gameObject.activeInHierarchy) continue;
                Add(ref b, ref any, inv * bc.transform.localToWorldMatrix, new Bounds(bc.center, bc.size));
            }
            return any;   // в единицах frame (с его масштабом); в метры переводит Compute
        }

        private static void Add(ref Bounds b, ref bool any, Matrix4x4 m, Bounds local)
        {
            var c = local.center; var e = local.extents;
            for (int i = 0; i < 8; i++)
            {
                var p = m.MultiplyPoint3x4(c + new Vector3((i & 1) != 0 ? e.x : -e.x, (i & 2) != 0 ? e.y : -e.y, (i & 4) != 0 ? e.z : -e.z));
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }

        // Верх матраса: первый твёрдый коллайдер под лучом от 1,1 м над полом, не выше 0,85 м над полом.
        private static float Surface(Vector3 mid, float floor)
        {
            var from = new Vector3(mid.x, floor + 1.1f, mid.z);
            var hits = Physics.RaycastAll(from, Vector3.down, 1.6f, -1, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            foreach (var h in hits)
            {
                float y = h.point.y;
                if (y - floor > 0.85f || y - floor < -0.3f) continue;
                if (y > best) best = y;
            }
            return best > float.NegativeInfinity ? best : floor + 0.45f;
        }

        // Расстояние до стены по направлению (без коллайдеров самой кровати); 99 — нет до 2,5 м.
        private static float Wall(Vector3 mid, Vector3 dir, Transform bed)
        {
            var from = mid + Vector3.up * 0.35f;
            float best = 99f;
            foreach (var h in Physics.RaycastAll(from, dir, 2.5f, -1, QueryTriggerInteraction.Ignore))
            {
                if (h.collider == null || h.collider.transform.IsChildOf(bed) || h.collider.TryCast<CharacterController>() != null) continue;   // не кровать и не сам игрок
                if (h.distance < best) best = h.distance;
            }
            return best;
        }

        // У остальных: строка занятости спящего → середина тела и поворот головы.
        internal static bool Parse(string busy, out Vector3 mid, out float yaw)
        {
            mid = default; yaw = 0f;
            if (busy == null || !busy.StartsWith("bed:", StringComparison.Ordinal)) return false;
            var p = busy.Substring(4).Split(';');
            if (p.Length != 4) return false;
            var f = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(p[i], NumberStyles.Float, CultureInfo.InvariantCulture, out f[i])) return false;
            mid = new Vector3(f[0], f[1], f[2]);
            yaw = f[3];
            return true;
        }

        private static string Fmt(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:0.00}; {1:0.00}; {2:0.00})", v.x, v.y, v.z);
    }
}
