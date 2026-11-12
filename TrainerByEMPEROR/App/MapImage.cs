using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TrainerByEMPEROR.App
{
    // Картинка карты из ответа игры (карта игры слоями или схема рельефа) и перевод мира в пиксели картинки.
    internal static class MapImage
    {
        public static (double W, double H, double[] Affine) Fill(Panel target, JsonElement map)
        {
            if (map.TryGetProperty("kind", out var kind) && kind.GetString() == "game") return Game(target, map);
            return Relief(target, map);
        }

        public static BitmapSource Decode(string base64)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(Convert.FromBase64String(base64));
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static (double, double, double[]) Relief(Panel target, JsonElement r)
        {
            double size = r.GetProperty("size").GetInt32();
            double minX = r.GetProperty("minX").GetDouble(), maxX = r.GetProperty("maxX").GetDouble();
            double minZ = r.GetProperty("minZ").GetDouble(), maxZ = r.GetProperty("maxZ").GetDouble();
            target.Children.Add(new Image { Source = Decode(r.GetProperty("jpg").GetString()), Width = size, Height = size, Stretch = Stretch.Fill });
            return (size, size, new[]
            {
                size / (maxX - minX), 0, -minX * size / (maxX - minX),
                0, -size / (maxZ - minZ), maxZ * size / (maxZ - minZ),
            });
        }

        private static (double, double, double[]) Game(Panel target, JsonElement r)
        {
            double w = r.GetProperty("width").GetDouble();
            double h = r.GetProperty("height").GetDouble();
            var m = r.GetProperty("affine").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            foreach (var l in r.GetProperty("layers").EnumerateArray())
            {
                BitmapSource src = Decode(l.GetProperty("png").GetString());
                double u0 = l.GetProperty("u0").GetDouble(), v0 = l.GetProperty("v0").GetDouble();
                double u1 = l.GetProperty("u1").GetDouble(), v1 = l.GetProperty("v1").GetDouble();
                if (u0 > 0.001 || v0 > 0.001 || u1 < 0.999 || v1 < 0.999)
                {
                    int pw = src.PixelWidth, ph = src.PixelHeight;
                    var rect = new Int32Rect((int)(Math.Clamp(u0, 0, 1) * pw), (int)((1 - Math.Clamp(v1, 0, 1)) * ph),
                        Math.Max(1, (int)((Math.Clamp(u1, 0, 1) - Math.Clamp(u0, 0, 1)) * pw)), Math.Max(1, (int)((Math.Clamp(v1, 0, 1) - Math.Clamp(v0, 0, 1)) * ph)));
                    src = new CroppedBitmap(src, rect);
                }
                var img = new Image
                {
                    Source = src,
                    Width = l.GetProperty("w").GetDouble(),
                    Height = l.GetProperty("h").GetDouble(),
                    Stretch = Stretch.Fill,
                    Opacity = l.GetProperty("opacity").GetDouble(),
                };
                Canvas.SetLeft(img, l.GetProperty("x").GetDouble());
                Canvas.SetTop(img, l.GetProperty("y").GetDouble());
                target.Children.Add(img);
            }
            return (w, h, m);
        }
    }
}
