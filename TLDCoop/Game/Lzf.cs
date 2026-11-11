using System;
using System.IO;

namespace TLDCoop.Game
{
    // Формат LZF, которым игра сжимает файлы сохранений (CLZF2).
    internal static class Lzf
    {
        public static byte[] Decompress(byte[] d)
        {
            var o = new MemoryStream(d.Length * 4);
            var buf = o.GetBuffer();
            int len = 0, i = 0;
            void Ensure(int extra)
            {
                if (len + extra <= buf.Length) return;
                var nb = new byte[Math.Max(buf.Length * 2, len + extra)];
                Buffer.BlockCopy(buf, 0, nb, 0, len);
                buf = nb;
            }
            while (i < d.Length)
            {
                int c = d[i++];
                if (c < 32)
                {
                    int l = c + 1;
                    Ensure(l);
                    Buffer.BlockCopy(d, i, buf, len, l);
                    len += l;
                    i += l;
                }
                else
                {
                    int l = c >> 5;
                    if (l == 7) l += d[i++];
                    int r = len - ((c & 0x1f) << 8) - 1 - d[i++];
                    l += 2;
                    Ensure(l);
                    for (int k = 0; k < l; k++) buf[len + k] = buf[r + k];
                    len += l;
                }
            }
            var result = new byte[len];
            Buffer.BlockCopy(buf, 0, result, 0, len);
            return result;
        }

        // Без настоящего сжатия: только блоки «как есть». Игра такой файл читает так же.
        public static byte[] StoreOnly(byte[] d)
        {
            var o = new MemoryStream(d.Length + d.Length / 32 + 1);
            for (int i = 0; i < d.Length; i += 32)
            {
                int l = Math.Min(32, d.Length - i);
                o.WriteByte((byte)(l - 1));
                o.Write(d, i, l);
            }
            return o.ToArray();
        }
    }
}
