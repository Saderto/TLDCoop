using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using MelonLoader;
using TLDTrainer.Shared;

namespace TLDTrainer
{
    // Принимает команды от окна трейнера по локальному TCP (только 127.0.0.1).
    // Протокол: одна строка JSON — запрос, одна строка JSON — ответ.
    internal static class Server
    {
        public static volatile bool InGame;

        private static TcpListener _listener;

        public static void Start()
        {
            var t = new Thread(Listen) { IsBackground = true, Name = "TLDTrainer server" };
            t.Start();
        }

        private static void Listen()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Features.Port);
                _listener.Start();
                MelonLogger.Msg($"Жду подключения окна трейнера на порту {Features.Port}");
                while (true)
                {
                    var client = _listener.AcceptTcpClient();
                    var t = new Thread(() => Serve(client)) { IsBackground = true, Name = "TLDTrainer client" };
                    t.Start();
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("Сервер трейнера остановлен: " + e.Message);
            }
        }

        private static void Serve(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
            {
                try
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string reply;
                        try
                        {
                            reply = JsonSerializer.Serialize(Handle(line));
                        }
                        catch (Exception e)
                        {
                            var msg = e is AggregateException ae && ae.InnerException != null ? ae.InnerException.Message : e.Message;
                            reply = JsonSerializer.Serialize(new { ok = false, error = msg });
                        }
                        writer.WriteLine(reply);
                    }
                }
                catch (IOException)
                {
                    // окно закрыли
                }
            }
        }

        private static object Handle(string line)
        {
            using var doc = JsonDocument.Parse(line);
            var r = doc.RootElement;
            string cmd = r.GetProperty("cmd").GetString();
            switch (cmd)
            {
                case "hello":
                    return new { ok = true, version = Features.Version };

                case "state":
                    return new { ok = true, inGame = InGame, values = State.Snapshot() };

                case "set":
                    State.Set(r.GetProperty("id").GetString(), (float)r.GetProperty("value").GetDouble());
                    return new { ok = true };

                case "action":
                {
                    string id = r.GetProperty("id").GetString();
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Cheats.RunAction(id);
                        return null;
                    });
                    return new { ok = true };
                }

                case "settime":
                {
                    int hour = r.GetProperty("hour").GetInt32();
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Cheats.SetTime(hour);
                        return null;
                    });
                    return new { ok = true };
                }

                case "weather":
                {
                    int stage = r.GetProperty("stage").GetInt32();
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Cheats.SetWeather(stage);
                        return null;
                    });
                    return new { ok = true };
                }

                case "spawn":
                {
                    string item = r.GetProperty("item").GetString();
                    int count = r.GetProperty("count").GetInt32();
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Cheats.Spawn(item, count);
                        return null;
                    });
                    return new { ok = true };
                }

                case "items":
                    return MainThread.Run(Catalog.Status, 20000);

                case "items_rebuild":
                    MainThread.Run(() =>
                    {
                        Catalog.Rebuild();
                        return null;
                    });
                    return new { ok = true };

                case "blueprints":
                    RequireGame();
                    return new { ok = true, items = MainThread.Run(Catalog.Blueprints) };

                case "unlockbp":
                {
                    int index = r.GetProperty("index").GetInt32();
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Catalog.UnlockBlueprint(index);
                        return null;
                    });
                    return new { ok = true };
                }

                case "unlockallbp":
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Catalog.UnlockAllBlueprints();
                        return null;
                    });
                    return new { ok = true };

                case "recipes":
                    RequireGame();
                    return new { ok = true, items = MainThread.Run(Recipes.List) };

                case "unlockrecipe":
                {
                    int index = r.GetProperty("index").GetInt32();
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Recipes.Unlock(index);
                        return null;
                    });
                    return new { ok = true };
                }

                case "unlockallrecipes":
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Recipes.UnlockAll();
                        return null;
                    });
                    return new { ok = true };

                case "badges":
                    return new { ok = true, items = MainThread.Run(Badges.List), feats = MainThread.Run(Badges.FeatList) };

                case "unlockfeat":
                {
                    int id = r.GetProperty("id").GetInt32();
                    MainThread.Run(() =>
                    {
                        Badges.UnlockFeat(id);
                        return null;
                    });
                    return new { ok = true };
                }

                case "unlockallfeats":
                    MainThread.Run(() =>
                    {
                        Badges.UnlockAllFeats();
                        return null;
                    });
                    return new { ok = true };

                case "unlockbadge":
                {
                    int id = r.GetProperty("id").GetInt32();
                    MainThread.Run(() =>
                    {
                        Badges.Unlock(id);
                        return null;
                    });
                    return new { ok = true };
                }

                case "unlockallbadges":
                    MainThread.Run(() =>
                    {
                        Badges.UnlockAll();
                        return null;
                    });
                    return new { ok = true };

                case "mappos":
                    RequireGame();
                    return MainThread.Run(MapService.Position);

                case "maprender":
                    RequireGame();
                {
                    bool force = r.TryGetProperty("force", out var fp) && fp.ValueKind == JsonValueKind.True;
                    bool game = r.TryGetProperty("source", out var sp) && sp.GetString() == "game";
                    return MainThread.Run(() => game ? GameMap.Request(force) : MapService.Render(force));
                }

                case "skills":
                    RequireGame();
                    return new { ok = true, items = MainThread.Run(Skills.List) };

                case "skilllevel":
                {
                    int id = r.GetProperty("id").GetInt32();
                    int delta = r.GetProperty("delta").GetInt32();
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Skills.Change(id, delta);
                        return null;
                    });
                    return new { ok = true };
                }

                case "mapsprites":
                {
                    RequireGame();
                    var names = new List<string>();
                    foreach (var n in r.GetProperty("names").EnumerateArray()) names.Add(n.GetString());
                    return MainThread.Run(() => MapSprites.Get(names.ToArray()), 20000);
                }

                case "mapmarkers":
                    RequireGame();
                {
                    bool refresh = r.TryGetProperty("refresh", out var rf) && rf.ValueKind == JsonValueKind.True;
                    return new { ok = true, markers = MainThread.Run(() => MapService.Markers(refresh), 20000) };
                }

                case "teleport":
                {
                    float x = (float)r.GetProperty("x").GetDouble();
                    float z = (float)r.GetProperty("z").GetDouble();
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        MapService.Teleport(x, z);
                        return null;
                    });
                    return new { ok = true };
                }

                case "overlaykeys":
                    if (r.TryGetProperty("overlay", out var ovk) && r.TryGetProperty("mini", out var mvk))
                        OverlayKeys.Set(ovk.GetInt32(), mvk.GetInt32());
                    return new { ok = true, overlay = OverlayKeys.OverlayPresses, mini = OverlayKeys.MiniPresses };

                case "binds":
                    return new { ok = true, binds = Hotkeys.Current(), defaults = Hotkeys.Defaults() };

                case "setbinds":
                {
                    var map = new System.Collections.Generic.Dictionary<string, string>();
                    foreach (var p in r.GetProperty("binds").EnumerateObject()) map[p.Name] = p.Value.GetString();
                    Hotkeys.Apply(map);
                    return new { ok = true };
                }

                case "quests":
                    RequireGame();
                    return new { ok = true, missions = MainThread.Run(Quests.List) };

                case "completeobjective":
                {
                    string id = r.GetProperty("id").GetString();
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Quests.CompleteObjective(id);
                        return null;
                    });
                    return new { ok = true };
                }

                case "completemission":
                {
                    string id = r.GetProperty("id").GetString();
                    RequireGame();
                    MainThread.Run(() =>
                    {
                        Quests.CompleteMission(id);
                        return null;
                    });
                    return new { ok = true };
                }

                default:
                    throw new ArgumentException("Неизвестная команда " + cmd);
            }
        }

        private static void RequireGame()
        {
            if (!InGame) throw new InvalidOperationException("Сначала загрузите сохранение в игре");
        }
    }
}
