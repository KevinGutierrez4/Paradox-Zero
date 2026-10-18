#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneMoreTime.Editor
{
    // Wolf fur is foreground even when white. Only discard detached sheet fragments.
    // Two source pieces have damaged alpha but still contain their original RGB fur.
    internal static class WolfSpriteRepair
    {
        internal static Rect Prepare(Texture2D texture, string folder, string file)
        {
            int w = texture.width, h = texture.height;
            var pixels = texture.GetPixels32();
            bool repair = folder == "01_lobo_blanco" && (file == "pata_delantera_muslo" || file == "cola_base");
            if (repair)
            {
                var boundary = new bool[pixels.Length];
                for (int i = 0; i < pixels.Length; i++)
                    boundary[i] = pixels[i].r < 100 && pixels[i].g < 100 && pixels[i].b < 100;
                // Coordinates on the supplied cutouts, measured from the top-left.
                // Seal the open joint cuts for flood-fill without drawing new RGB pixels.
                if (file == "pata_delantera_muslo")
                { Seal(67, 8, 107, 8); Seal(10, 108, 58, 112); }
                else Seal(96, 33, 92, 68);
                var exterior = new bool[pixels.Length]; var queue = new Queue<int>();
                for (int x = 0; x < w; x++) { Seed(x); Seed((h - 1) * w + x); }
                for (int y = 0; y < h; y++) { Seed(y * w); Seed(y * w + w - 1); }
                while (queue.Count > 0)
                {
                    int p = queue.Dequeue(), x = p % w, y = p / w;
                    if (x > 0) Seed(p - 1); if (x + 1 < w) Seed(p + 1);
                    if (y > 0) Seed(p - w); if (y + 1 < h) Seed(p + w);
                }
                for (int i = 0; i < pixels.Length; i++) if (boundary[i] || !exterior[i]) pixels[i].a = 255;
                void Seed(int p) { if (!boundary[p] && !exterior[p]) { exterior[p] = true; queue.Enqueue(p); } }
                void Seal(int ax, int ay, int bx, int by)
                {
                    int steps = Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
                    for (int s = 0; s <= steps; s++)
                    {
                        int x = Mathf.RoundToInt(Mathf.Lerp(ax, bx, (float)s / steps));
                        int y = h - 1 - Mathf.RoundToInt(Mathf.Lerp(ay, by, (float)s / steps));
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                            if (x + dx >= 0 && x + dx < w && y + dy >= 0 && y + dy < h) boundary[(y + dy) * w + x + dx] = true;
                    }
                }
            }
            var visited = new bool[pixels.Length]; var parts = new List<List<int>>();
            // Adjacent sheet cells touch these cutouts through pale crop-guide pixels.
            for (int p = 0; p < pixels.Length; p++)
            {
                int x = p % w, topY = h - 1 - p / w;
                if ((folder == "01_lobo_blanco" && file == "torso" && x >= 115 && x <= 155 && topY >= 138)
                    || (folder == "05_lobo_azul" && file == "pata_trasera_sup_1" && topY >= 62)
                    || (folder == "05_lobo_azul" && file == "pata_delantera_sup_1" && topY >= 60)) pixels[p].a = 0;
            }
            var pending = new Queue<int>();
            for (int start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || !Core(start)) continue;
                var part = new List<int>(); Visit(start);
                while (pending.Count > 0)
                {
                    int p = pending.Dequeue(), x = p % w, y = p / w; part.Add(p);
                    if (x > 0) Visit(p - 1); if (x + 1 < w) Visit(p + 1);
                    if (y > 0) Visit(p - w); if (y + 1 < h) Visit(p + w);
                }
                parts.Add(part);
                void Visit(int p) { if (!visited[p] && Core(p)) { visited[p] = true; pending.Enqueue(p); } }
            }
            bool Core(int p)
            {
                var c = pixels[p];
                return c.a >= 100 && !(c.r >= 225 && c.g >= 225 && c.b >= 225 && Math.Max(c.r, Math.Max(c.g, c.b)) - Math.Min(c.r, Math.Min(c.g, c.b)) < 8);
            }
            parts.Sort((a, b) => b.Count.CompareTo(a.Count));
            if (parts.Count == 0) throw new InvalidOperationException("Empty wolf sprite: " + file);
            var keep = new bool[pixels.Length];
            bool split = (folder == "05_lobo_azul" && file.Contains("_inf_")) || (folder == "01_lobo_blanco" && file == "pata_delantera_muslo");
            int count = split ? Math.Min(2, parts.Count) : 1;
            for (int j = 0; j < count; j++)
                if (j == 0 || parts[j].Count > parts[0].Count * 0.15f) foreach (int p in parts[j]) keep[p] = true;
            // Preserve white fur between the outline edges even where a joint is open.
            // Flood-fill alone leaks through those joints and deletes the entire white interior.
            var rowMin = new int[h]; var rowMax = new int[h]; var colMin = new int[w]; var colMax = new int[w];
            for (int y = 0; y < h; y++) { rowMin[y] = w; rowMax[y] = -1; }
            for (int x = 0; x < w; x++) { colMin[x] = h; colMax[x] = -1; }
            for (int p = 0; p < keep.Length; p++) if (keep[p])
            {
                int x = p % w, y = p / w;
                rowMin[y] = Math.Min(rowMin[y], x); rowMax[y] = Math.Max(rowMax[y], x);
                colMin[x] = Math.Min(colMin[x], y); colMax[x] = Math.Max(colMax[x], y);
            }
            for (int p = 0; p < keep.Length; p++)
            {
                int x = p % w, y = p / w;
                if (pixels[p].a >= 100 && ((x >= rowMin[y] && x <= rowMax[y]) || (y >= colMin[x] && y <= colMax[x]))) keep[p] = true;
            }
            if (folder == "05_lobo_azul" && file.Contains("_inf_"))
            {
                // Close the empty sheet rows between shin and paw without inventing pixels.
                var joined = new Color32[pixels.Length]; var joinedMask = new bool[pixels.Length]; int destY = 0;
                for (int y = 0; y < h; y++)
                {
                    bool occupied = false;
                    for (int x = 0; x < w; x++) if (keep[y * w + x]) { occupied = true; break; }
                    if (!occupied) continue;
                    for (int x = 0; x < w; x++) if (keep[y * w + x])
                    { joined[destY * w + x] = pixels[y * w + x]; joinedMask[destY * w + x] = true; }
                    destY++;
                }
                pixels = joined; keep = joinedMask;
            }
            int minX = w, minY = h, maxX = 0, maxY = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (!keep[i]) { pixels[i].a = 0; continue; }
                minX = Math.Min(minX, i % w); maxX = Math.Max(maxX, i % w);
                minY = Math.Min(minY, i / w); maxY = Math.Max(maxY, i / w);
            }
            texture.SetPixels32(pixels); texture.Apply();
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
#endif
