using System;
using MelonLoader;
using UnityEngine;

namespace TLDCoop.Game
{
    // Волокуша, которую тащит другой игрок. Настоящая волокуша у остальных убрана (Game/WorldExtra.cs, «TAKE»),
    // а за его фигурой едет её копия без скриптов и коллайдеров (как вещи в руках, Game/HeldItems.cs).
    // Тот, кто тащит, присылает, где волокуша стоит относительно него. Если игра её прячет, копия
    // сама волочится за фигурой: держится на длине оглоблей позади и ложится на землю.
    public class CarryVisual
    {
        private const float Length = 1.6f;

        private string _key = "";
        private float _retryAt;
        private bool _missLogged;
        private GameObject _go;
        private bool _placed;
        private Vector3 _pos;
        private Quaternion _rot = Quaternion.identity;

        // ---------- Своя волокуша ----------

        public static string Own(Vector3 playerPos, out bool hasPose, out Vector3 rel, out Quaternion rot)
        {
            hasPose = false;
            rel = Vector3.zero;
            rot = Quaternion.identity;
            try
            {
                var sys = Il2CppTLD.BigCarry.BigCarrySystem.Instance;
                var it = sys?.CarriedItem;
                if (it == null || sys.IsDroppingItem || it.TryCast<Il2CppTLD.BigCarry.TravoisBigCarryItem>() == null) return "";
                string key = it.m_AssetReference?.AssetGUID ?? "";
                if (key.Length == 0) return "";
                if (it.gameObject.activeInHierarchy)
                {
                    var t = it.transform;
                    rel = t.position - playerPos;
                    if (rel.sqrMagnitude < 36f)
                    {
                        rot = t.rotation;
                        hasPose = true;
                    }
                    else rel = Vector3.zero;
                }
                return key;
            }
            catch { return ""; }
        }

        // ---------- Чужая волокуша ----------

        public void Update(string key, Vector3 anchor, bool hasPose, Vector3 rel, Quaternion rot, float dt)
        {
            key ??= "";
            if (key != _key)
            {
                Clear();
                _key = key;
                _retryAt = 0f; _missLogged = false;
                if (key.Length > 0) Build(key);
            }
            // Модель грузится без остановки кадра (HeldItems.Template вернёт её, когда догрузится): пробуем ещё раз.
            if (_go == null && _key.Length > 0 && Time.unscaledTime >= _retryAt) { _retryAt = Time.unscaledTime + 0.5f; Build(_key); }
            if (_go == null) return;

            Vector3 tp;
            Quaternion tr;
            if (hasPose)
            {
                tp = anchor + rel;
                tr = rot;
            }
            else
            {
                // Тянется за фигурой: не ближе 0,3 м и не дальше длины оглоблей.
                var from = _placed ? _pos : anchor + new Vector3(0f, 0f, -Length);
                var d = from - anchor;
                d.y = 0f;
                if (d.sqrMagnitude < 0.09f) d = new Vector3(0f, 0f, -Length);
                else if (d.magnitude > Length) d = d.normalized * Length;
                tp = anchor + d;
                tp.y = Ground(tp, anchor.y);
                var look = anchor - tp;
                tr = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look.normalized, Vector3.up) : _rot;
            }

            if (!_placed || (tp - _pos).sqrMagnitude > 100f)
            {
                _pos = tp;
                _rot = tr;
                _placed = true;
            }
            else
            {
                float k = 1f - (float)Math.Exp(-12f * dt);
                _pos = Vector3.Lerp(_pos, tp, k);
                _rot = Quaternion.Slerp(_rot, tr, k);
            }
            _go.transform.SetPositionAndRotation(_pos, _rot);
        }

        private static float Ground(Vector3 p, float fallback)
        {
            try
            {
                if (Physics.Raycast(new Vector3(p.x, fallback + 2f, p.z), Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
                    return hit.point.y;
            }
            catch { }
            return fallback;
        }

        private void Build(string key)
        {
            var template = HeldItems.Template(key);
            if (template == null)
            {
                // В каталоге игры не нашлась: берём вид любой такой же волокуши в этой локации.
                foreach (var it in UnityEngine.Object.FindObjectsOfType<Il2CppTLD.BigCarry.BigCarryItem>())
                {
                    if (it == null || it.m_AssetReference?.AssetGUID != key) continue;
                    HeldItems.Remember(key, it.gameObject);
                    template = HeldItems.Template(key);
                    break;
                }
            }
            if (template == null)
            {
                if (!_missLogged) { _missLogged = true; MelonLogger.Msg("[модель] пока нет модели волокуши " + key + " (жду загрузку)"); }
                return;
            }
            _missLogged = false;
            _go = UnityEngine.Object.Instantiate(template);
            _go.name = "TLDCoop_Carry_" + key;
            if (!_go.activeSelf) _go.SetActive(true);
            _placed = false;
        }

        public void Clear()
        {
            if (_go != null) UnityEngine.Object.Destroy(_go);
            _go = null;
            _key = "";
            _placed = false;
        }
    }
}
