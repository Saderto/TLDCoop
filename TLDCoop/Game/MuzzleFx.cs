using System;
using System.Collections;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TLDCoop.Game
{
    // Вспышка и дым у дула, когда стреляет другой игрок. Берём те же эффекты, что игра показывает от первого лица:
    // вещь (GEAR_Rifle…) → её FPSItem → префаб оружия от первого лица → vp_FPSShooter.MuzzleFlashPrefab → vp_MuzzleFlash,
    // у которого лежат системы частиц muzzleFlashFX (вспышка) и muzzleSmokeFX (дым). Их копия ставится у дула фигуры
    // в обычный слой (от первого лица они в слое оружия, который видит только своя камера) и убирается через 5 с.
    // Что внутри (разобрал префабы): MuzzleFlashPrefab = PistolMuzzleFlash — плоская «звезда» вспышки (меш);
    // FX_WeaponMuzzleFlash — пустая система + искры FX_Sparks (10 штук, 10–20 м/с: сбоку выглядят как выстрел
    // дробью — убираем), свет Light_MuzzleFlash (0.05 с) и выключенный клуб дыма Fx_SmokePuff (включаем);
    // FX_WeaponMuzzleSmokeTrail — сам ничего не испускает, дым из дула рисует скрипт SmokeTrail линией —
    // его оставляем и запускаем (StartSmoke), остальные скрипты убираем.
    public static class MuzzleFx
    {
        private class Fx { public GameObject Flash, Smoke, Star; }

        private static readonly Dictionary<string, Fx> Cache = new();
        private static readonly HashSet<string> Missing = new();
        private static GameObject _holder;

        // Префабы, которые сейчас грузятся (без WaitForCompletion: во время загрузки сцены он может повесить игру).
        private static readonly HashSet<string> Loading = new();

        // Готовая вспышка или null, пока грузится (тогда выстрел без вспышки) или если её нет.
        private static Fx Get(string prefab)
        {
            if (string.IsNullOrEmpty(prefab) || Missing.Contains(prefab)) return null;
            if (Cache.TryGetValue(prefab, out var fx)) return fx;
            if (Loading.Add(prefab)) MelonCoroutines.Start(LoadAsync(prefab));
            return null;
        }

        private static IEnumerator LoadAsync(string prefab)
        {
            Fx fx = null;
            GameObject src = null, weapon = null;
            UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> h = default;
            bool ok;
            try
            {
                var key = new Il2CppSystem.Object(IL2CPP.ManagedStringToIl2Cpp(prefab));
                h = Addressables.LoadAssetAsync<GameObject>(key);
                ok = true;
            }
            catch (Exception e) { MelonLogger.Warning($"[выстрел] {prefab}: {e.Message}"); ok = false; }
            if (ok)
            {
                IEnumerator w = Wait(h, prefab);
                while (w.MoveNext()) yield return null;
                src = Result(h);
                Il2CppSystem.Object rkey = null;
                try
                {
                    var fps = src != null ? src.GetComponent<GearItem>()?.GetFPSItem() : null;
                    var aref = fps?.m_FPSWeaponPrefab;
                    if (aref == null) MelonLogger.Msg($"[выстрел] {prefab}: нет оружия от первого лица");
                    else rkey = aref.RuntimeKey;
                    if (rkey != null) h = Addressables.LoadAssetAsync<GameObject>(rkey);
                }
                catch (Exception e) { MelonLogger.Warning($"[выстрел] {prefab}: {e.Message}"); rkey = null; }
                if (rkey != null)
                {
                    w = Wait(h, prefab);
                    while (w.MoveNext()) yield return null;
                    weapon = Result(h);
                    if (weapon == null) MelonLogger.Msg($"[выстрел] {prefab}: оружие от первого лица не загрузилось");
                    else fx = Load(prefab, weapon);
                }
            }
            if (fx == null)
            {
                // У ружей из DLC и прочих своей вспышки может не найтись — берём от обычного ружья или револьвера.
                string base_ = prefab.StartsWith("GEAR_Rifle") || prefab == "GEAR_Shotgun" ? "GEAR_Rifle" : prefab.StartsWith("GEAR_Revolver") ? "GEAR_Revolver" : null;
                if (base_ != null && base_ != prefab)
                {
                    Get(base_);
                    while (Loading.Contains(base_)) yield return null;
                    if (Cache.TryGetValue(base_, out fx)) MelonLogger.Msg($"[выстрел] {prefab}: беру вспышку {base_}");
                }
            }
            Loading.Remove(prefab);
            if (fx == null) Missing.Add(prefab);
            else Cache[prefab] = fx;
        }

        // Ждём загрузку по кадрам; если долго — пишем в лог один раз и ждём дальше.
        private static IEnumerator Wait(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> h, string prefab)
        {
            float t0 = Time.realtimeSinceStartup;
            bool warned = false;
            while (true)
            {
                bool done;
                try { done = h.IsDone; } catch { done = true; }
                if (done) yield break;
                if (!warned && Time.realtimeSinceStartup - t0 > 30f) { warned = true; MelonLogger.Warning($"[выстрел] {prefab}: загрузка идёт дольше 30 с"); }
                yield return null;
            }
        }

        private static GameObject Result(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> h)
        {
            try
            {
                if (h.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded) return h.Result;
            }
            catch { }
            return null;
        }

        // Где лежат эффекты. На префабе оружия vp_FPSShooter.m_MuzzleFlash ещё пуст (игра создаёт его в Start
        // из MuzzleFlashPrefab), поэтому сначала MuzzleFlashPrefab → vp_MuzzleFlash (muzzleFlashFX, muzzleSmokeFX).
        // У ружей с руками (FPH_Rifle_…, vp_FPSWeapon.m_FirstPersonWeapon…Prefab) эффекты в списке
        // FirstPersonWeapon.m_FXInfos (имя → объект): берём те, что про вспышку и дым.
        // weapon — уже загруженный префаб оружия от первого лица (LoadAsync).
        private static Fx Load(string prefab, GameObject weapon)
        {
            try
            {
                if (_holder == null)
                {
                    _holder = new GameObject("TLDCoop_MuzzleFx");
                    _holder.SetActive(false);
                    UnityEngine.Object.DontDestroyOnLoad(_holder);
                }
                GameObject flash = null, smoke = null, star = null;
                string where = "";
                var seen = new List<string>();
                var shooter = weapon.GetComponentInChildren<vp_FPSShooter>(true);
                foreach (var go in new[] { shooter?.MuzzleFlashPrefab, shooter?.m_MuzzleFlash })
                {
                    if (go == null || flash != null) continue;
                    if (star == null && go.GetComponent<MeshRenderer>() != null) star = go;
                    var mf = go.GetComponentInChildren<vp_MuzzleFlash>(true);
                    if (mf != null) { flash = mf.muzzleFlashFX?.gameObject; smoke = mf.muzzleSmokeFX?.gameObject; }
                    if (flash == null && smoke == null) Pick(go, ref flash, ref smoke);
                    if (flash != null || smoke != null) where = "стрелок " + go.name;
                }
                if (flash == null && smoke == null)
                {
                    var mf = weapon.GetComponentInChildren<vp_MuzzleFlash>(true);
                    if (mf != null) { flash = mf.muzzleFlashFX?.gameObject; smoke = mf.muzzleSmokeFX?.gameObject; where = "оружие"; }
                }
                var fw = weapon.GetComponentInChildren<vp_FPSWeapon>(true);
                if (flash == null && smoke == null && fw != null)
                    foreach (var hands in new[] { fw.m_FirstPersonWeaponRightHandPrefab, fw.m_FirstPersonWeaponShoulderPrefab, fw.m_FirstPersonWeaponLeftHandPrefab })
                    {
                        if (hands == null) continue;
                        var fpw = hands.GetComponentInChildren<FirstPersonWeapon>(true);
                        if (fpw != null && fpw.m_FXInfos != null)
                            foreach (var fi in fpw.m_FXInfos)
                            {
                                if (fi == null || fi.m_GameObject == null) continue;
                                string n = (fi.m_Name ?? "") + "/" + fi.m_GameObject.name;
                                seen.Add(n);
                                bool isSmoke = n.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0;
                                bool isFlash = !isSmoke && (n.IndexOf("muzzle", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("flash", StringComparison.OrdinalIgnoreCase) >= 0);
                                if (isSmoke && smoke == null) smoke = fi.m_GameObject;
                                else if (isFlash && (flash == null || n.IndexOf("Corrected", StringComparison.OrdinalIgnoreCase) < 0 && flash.name.IndexOf("Corrected", StringComparison.OrdinalIgnoreCase) >= 0)) flash = fi.m_GameObject;
                            }
                        if (flash == null && smoke == null)
                        {
                            var mf = hands.GetComponentInChildren<vp_MuzzleFlash>(true);
                            if (mf != null) { flash = mf.muzzleFlashFX?.gameObject; smoke = mf.muzzleSmokeFX?.gameObject; }
                        }
                        if (flash == null && smoke == null) Pick(hands, ref flash, ref smoke);
                        if (flash != null || smoke != null) { where = "руки " + hands.name; break; }
                    }
                if (seen.Count > 0) MelonLogger.Msg($"[выстрел] {prefab}: эффекты рук: {string.Join(", ", seen)}");
                if (flash == null && smoke == null) { MelonLogger.Msg($"[выстрел] {prefab}: у оружия {weapon.name} нет вспышки"); return null; }
                var fx = new Fx { Flash = Copy(flash), Smoke = Copy(smoke), Star = MakeStar(star) };
                if (!HasParticles(fx.Flash) && !HasParticles(fx.Smoke)) { MelonLogger.Msg($"[выстрел] {prefab}: у вспышки ({where}) нет частиц"); return null; }
                MelonLogger.Msg($"[выстрел] {prefab}: вспышка {Describe(fx.Flash)}, дым {Describe(fx.Smoke)}, звезда {(fx.Star != null ? star.name : "нет")} ({where})");
                return fx;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[выстрел] {prefab}: {e.Message}");
                return null;
            }
        }

        // Системы частиц по именам: «smoke» — дым, «muzzle»/«flash»/«fire» — вспышка.
        private static void Pick(GameObject root, ref GameObject flash, ref GameObject smoke)
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                string n = ps.gameObject.name;
                if (n.IndexOf("smoke", StringComparison.OrdinalIgnoreCase) >= 0) smoke ??= ps.gameObject;
                else if (n.IndexOf("muzzle", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("flash", StringComparison.OrdinalIgnoreCase) >= 0
                         || n.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0) flash ??= ps.gameObject;
            }
        }

        // Пустая система без материала (корень FX_WeaponMuzzleFlash) ничего не рисует — не запускаем.
        private static bool Visible(ParticleSystem ps)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            return r != null && r.enabled && r.sharedMaterial != null;
        }

        private static bool HasParticles(GameObject go) => go != null && go.GetComponentsInChildren<ParticleSystem>(true).Length > 0;

        // Шаблон под выключенным родителем: у копии не запускаются скрипты, пока не понадобится.
        private static GameObject Copy(GameObject src)
        {
            if (src == null) return null;
            var go = UnityEngine.Object.Instantiate(src, _holder.transform);
            go.name = "Fx_" + src.name;
            // Скрипты оружия (vp_MuzzleFlash, LightTracking, ветер для частиц) без самого оружия падают или прячут
            // эффект — убираем; SmokeTrail (линия дыма) работает сам по себе — оставляем.
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                try { if (mb.TryCast<SmokeTrail>() == null) UnityEngine.Object.DestroyImmediate(mb); } catch { }
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                string n = t.gameObject.name;
                if (t != go.transform && n.IndexOf("spark", StringComparison.OrdinalIgnoreCase) >= 0) t.gameObject.SetActive(false);
                else if (n.IndexOf("SmokePuff", StringComparison.OrdinalIgnoreCase) >= 0) t.gameObject.SetActive(true);
            }
            foreach (var l in go.GetComponentsInChildren<Light>(true)) { l.intensity = 0f; l.range = 15f; }
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            SetLayer(go.transform, 0);
            return go;
        }

        // «Звезда» вспышки от первого лица плоская и смотрит вдоль ствола — сбоку её не видно. Из неё три
        // плоскости: поперёк ствола (как от первого лица) и две вдоль ствола, вытянутые вперёд от дула.
        private static GameObject MakeStar(GameObject src)
        {
            if (src == null) return null;
            try
            {
                var root = new GameObject("Fx_Star");
                root.transform.SetParent(_holder.transform, false);
                float s = Mathf.Max(0.05f, src.transform.localScale.x);   // в префабе 0.5: звезда ~0.29 м
                for (int i = 0; i < 3; i++)
                {
                    var go = UnityEngine.Object.Instantiate(src, root.transform);
                    foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                        try { UnityEngine.Object.DestroyImmediate(mb); } catch { }
                    go.name = "Star" + i;
                    var tr = go.transform;
                    if (i == 0) { tr.localPosition = Vector3.zero; tr.localRotation = Quaternion.identity; tr.localScale = Vector3.one * s; }
                    else
                    {
                        // Меш лежит в плоскости XY: поворот вокруг Y ставит его ось X вдоль ствола, вокруг X — ось Y.
                        tr.localPosition = new Vector3(0f, 0f, 0.29f * s);
                        tr.localRotation = i == 1 ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.Euler(90f, 0f, 0f);
                        tr.localScale = i == 1 ? new Vector3(s, s * 0.45f, s) : new Vector3(s * 0.45f, s, s);
                    }
                }
                SetLayer(root.transform, 0);
                return root;
            }
            catch (Exception e) { MelonLogger.Warning("[выстрел] звезда: " + e.Message); return null; }
        }

        private static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        private static string Describe(GameObject go)
        {
            if (go == null) return "нет";
            var ps = go.GetComponentsInChildren<ParticleSystem>(true);
            return $"{go.name} (систем {ps.Length}, масштаб {go.transform.localScale.x:0.##})";
        }

        // Начать загрузку заранее (фигура взяла оружие), чтобы к выстрелу вспышка была готова.
        public static void Preload(string prefab) => Get(prefab);

        // pos — дуло, dir — куда смотрит ствол.
        public static void Play(string prefab, Vector3 pos, Vector3 dir)
        {
            var fx = Get(prefab);
            if (fx == null || dir.sqrMagnitude < 1e-6f) return;
            var rot = Quaternion.LookRotation(dir);
            GameObject flash = null, smoke = null, star = null;
            try
            {
                if (fx.Flash != null)
                {
                    flash = UnityEngine.Object.Instantiate(fx.Flash, pos, rot);
                    flash.SetActive(true);
                    foreach (var ps in flash.GetComponentsInChildren<ParticleSystem>(false)) if (Visible(ps)) ps.Play(false);
                    UnityEngine.Object.Destroy(flash, 5f);
                }
                if (fx.Smoke != null)
                {
                    smoke = UnityEngine.Object.Instantiate(fx.Smoke, pos, rot);
                    smoke.SetActive(true);
                    foreach (var ps in smoke.GetComponentsInChildren<ParticleSystem>(false)) if (Visible(ps)) ps.Play(false);
                    UnityEngine.Object.Destroy(smoke, 5f);
                }
                if (fx.Star != null)
                {
                    star = UnityEngine.Object.Instantiate(fx.Star, pos, rot * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)));
                    star.SetActive(true);
                    UnityEngine.Object.Destroy(star, 1f);
                }
                MelonCoroutines.Start(Animate(flash, smoke, star));
            }
            catch (Exception e) { MelonLogger.Warning("[выстрел] вспышка: " + e.Message); }
        }

        // Свет и звезда — вспышка на 0.08 с с угасанием; дым — со следующего кадра (Start у SmokeTrail
        // сбрасывает его, поэтому StartSmoke после Start).
        private static IEnumerator Animate(GameObject flash, GameObject smoke, GameObject star)
        {
            var lights = new List<Light>();
            if (flash != null) foreach (var l in flash.GetComponentsInChildren<Light>(true)) lights.Add(l);
            var mats = new List<(Material m, string prop, Color c)>();
            if (star != null)
                foreach (var r in star.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var m = r.material;
                    string prop = m.HasProperty("_TintColor") ? "_TintColor" : m.HasProperty("_Color") ? "_Color" : null;
                    if (prop == null) continue;
                    var c = m.GetColor(prop);
                    if (c.a < 0.05f) c.a = 0.5f;
                    mats.Add((m, prop, c));
                }
            const float Dur = 0.08f;
            float t0 = Time.time;
            bool smoked = false;
            while (true)
            {
                float k = Mathf.Clamp01((Time.time - t0) / Dur);
                try
                {
                    foreach (var l in lights) if (l != null) l.intensity = 2f * (1f - k);
                    foreach (var (m, prop, c) in mats) if (m != null) m.SetColor(prop, new Color(c.r, c.g, c.b, c.a * (1f - k)));
                    if (!smoked && Time.time > t0)
                    {
                        smoked = true;
                        if (smoke != null) foreach (var st in smoke.GetComponentsInChildren<SmokeTrail>(true)) st.StartSmoke();
                    }
                }
                catch { yield break; }
                if (k >= 1f && smoked) break;
                yield return null;
            }
            try
            {
                if (star != null) UnityEngine.Object.Destroy(star);
                foreach (var (m, _, _) in mats) if (m != null) UnityEngine.Object.Destroy(m);
            }
            catch { }
        }
    }
}
