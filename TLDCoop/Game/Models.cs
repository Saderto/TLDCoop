using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;

namespace TLDCoop.Game
{
    // Модель игрока: персонажи WINTERMUTE (Game/Characters.cs), если рядом с модом лежит characters.bin,
    // иначе модель из пакета SkyCoop (Mods/TLDCoop/multiplayerstuff.unity3d, префаб «multiplayerPlayer»).
    // Пакет собран в старой Unity 2018, игра на Unity 6: он может не загрузиться или выглядеть
    // неправильно. Тогда остаётся простая фигура (см. RemotePlayer).
    public static class Models
    {
        public const string BundlePath = "Mods/TLDCoop/multiplayerstuff.unity3d";

        // Если пакета нет, мод сам скачивает его из официального релиза SkyCoop 13.2 на GitHub
        // и проверяет контрольные суммы.
        private const string ReleaseUrl = "https://github.com/Filigrani/SkyCoop/releases/download/v13.2/SkyCoopRelease.zip";
        private const string ReleaseSha256 = "6fee0723ebd72af22c5362aa7de966881875962305599dc38f321118e0b661b2";
        private const string BundleSha256 = "d1ea1fe36b248609ee7937f2477597b9e6a279f808f16e39f1793d81bc318399";
        private static Task _download;
        private static volatile string _downloadState;

        public static GameObject PlayerPrefab { get; private set; }
        public static string State { get; private set; } = UI.L.T("не загружена", "not loaded");

        private static AssetBundleCreateRequest _bundleReq;
        private static AssetBundleRequest _assetReq;
        private static AssetBundle _bundle;
        private static bool _started, _done, _chars, _charsTried;

        // Загрузка идёт в фоне и проверяется раз в кадр, чтобы игра не замирала.
        public static void Tick()
        {
            if (_done) return;
            try
            {
                if (_chars)
                {
                    if (Characters.Poll(out var err))
                    {
                        if (err == null) { Finish(Characters.State); return; }
                        MelonLogger.Warning("[модель] персонажи: " + err);
                        _chars = false;   // дальше — модель SkyCoop, как раньше
                    }
                    else { State = Characters.State; return; }
                }
                if (!_started)
                {
                    _started = true;
                    if (!Config.UseModel)
                    {
                        Finish(UI.L.T("выключена в настройках", "turned off in settings"));
                        return;
                    }
                    if (Characters.Available && !_charsTried)
                    {
                        _charsTried = true;
                        _chars = true;
                        _started = false;
                        Characters.StartLoad();
                        return;
                    }
                    if (!File.Exists(BundlePath))
                    {
                        _download = Task.Run(Download);
                        return;
                    }
                    StartLoad();
                    return;
                }
                if (_download != null)
                {
                    State = _downloadState ?? UI.L.T("скачивается…", "downloading…");
                    if (!_download.IsCompleted) return;
                    var error = _download.Exception?.GetBaseException().Message;
                    _download = null;
                    if (error != null || !File.Exists(BundlePath))
                    {
                        Finish(UI.L.T("не удалось скачать: ", "download failed: ") + (error ?? UI.L.T("файла нет", "no file")));
                        return;
                    }
                    StartLoad();
                    return;
                }
                if (_bundleReq != null)
                {
                    if (!_bundleReq.isDone) return;
                    _bundle = _bundleReq.assetBundle;
                    _bundleReq = null;
                    if (_bundle == null)
                    {
                        Finish(UI.L.T("Unity не приняла пакет (", "Unity rejected the bundle (") + Application.unityVersion + ")");
                        return;
                    }
                    _assetReq = _bundle.LoadAssetAsync("multiplayerPlayer", Il2CppType.Of<GameObject>());
                    return;
                }
                if (_assetReq != null)
                {
                    if (!_assetReq.isDone) return;
                    var asset = _assetReq.asset;
                    _assetReq = null;
                    PlayerPrefab = asset != null ? asset.TryCast<GameObject>() : null;
                    Finish(PlayerPrefab != null ? UI.L.T("загружена", "loaded") : UI.L.T("в пакете нет multiplayerPlayer", "no multiplayerPlayer in the bundle"));
                }
            }
            catch (Exception e)
            {
                Finish(UI.L.T("ошибка: ", "error: ") + e.Message);
            }
        }

        private static void StartLoad()
        {
            _bundleReq = AssetBundle.LoadFromFileAsync(BundlePath);
            State = UI.L.T("загружается…", "loading…");
        }

        private static void Download()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BundlePath));
            string zip = BundlePath + ".zip.part";
            MelonLogger.Msg("[модель] скачиваю " + ReleaseUrl);
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            using (var resp = http.GetAsync(ReleaseUrl, HttpCompletionOption.ResponseHeadersRead).Result)
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? 0;
                using var src = resp.Content.ReadAsStreamAsync().Result;
                using var dst = File.Create(zip);
                var buf = new byte[1 << 16];
                long got = 0;
                int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    got += n;
                    _downloadState = total > 0 ? UI.L.T("скачивается… ", "downloading… ") + $"{got * 100 / total}%" : UI.L.T("скачивается… ", "downloading… ") + $"{got / 1048576} " + UI.L.T("МБ", "MB");
                }
            }
            try
            {
                if (Sha256(zip) != ReleaseSha256) throw new IOException("контрольная сумма архива не совпала");
                _downloadState = UI.L.T("распаковывается…", "unpacking…");
                string tmp = BundlePath + ".part";
                using (var archive = ZipFile.OpenRead(zip))
                {
                    var entry = archive.GetEntry("multiplayerstuff.unity3d") ?? throw new IOException("в архиве нет пакета");
                    entry.ExtractToFile(tmp, true);
                }
                if (Sha256(tmp) != BundleSha256)
                {
                    File.Delete(tmp);
                    throw new IOException("контрольная сумма пакета не совпала");
                }
                File.Move(tmp, BundlePath, true);
                MelonLogger.Msg("[модель] пакет скачан");
            }
            finally
            {
                try { File.Delete(zip); } catch { }
            }
        }

        private static string Sha256(string path)
        {
            using var sha = SHA256.Create();
            using var f = File.OpenRead(path);
            return Convert.ToHexString(sha.ComputeHash(f)).ToLowerInvariant();
        }

        private static void Finish(string state)
        {
            _done = true;
            State = state;
            MelonLogger.Msg("[модель] " + state);
        }

        // Материалы из старой Unity часто ссылаются на шейдеры, которых в игре нет (розовые объекты).
        // Ставим им рабочий шейдер, сохраняя текстуру и цвет.
        public static void FixMaterials(GameObject go, Shader fallback)
        {
            if (fallback == null) return;
            int fixedCount = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    var s = m.shader;
                    if (s != null && s.isSupported && s.name != "Hidden/InternalErrorShader") continue;
                    Texture tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    Color color = m.HasProperty("_Color") ? m.color : Color.white;
                    m.shader = fallback;
                    if (tex != null) m.SetTexture("_MainTex", tex);
                    m.color = color;
                    fixedCount++;
                }
            }
            if (fixedCount > 0) MelonLogger.Msg($"[модель] заменён шейдер у {fixedCount} материалов на {fallback.name}");
        }
    }
}
