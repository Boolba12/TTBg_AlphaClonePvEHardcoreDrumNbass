using System.Collections.Generic;

public static class MapInteriorHoleUtility
{
    private sealed class CacheEntry
    {
        public int generationVersion;
        public bool[,] mask;
    }

    private static readonly Dictionary<MapGenerator, CacheEntry> MaskCacheByGenerator = new Dictionary<MapGenerator, CacheEntry>();

    public static bool[,] BuildInteriorHoleMask(MapGenerator mapGenerator)
    {
        if (mapGenerator == null)
            return new bool[0, 0];

        int width = mapGenerator.width;
        int height = mapGenerator.height;

        if (width <= 0 || height <= 0)
            return new bool[0, 0];

        if (!mapGenerator.HasGeneratedData)
            return new bool[width, height];

        if (MaskCacheByGenerator.TryGetValue(mapGenerator, out CacheEntry cached) &&
            cached != null &&
            cached.mask != null &&
            cached.generationVersion == mapGenerator.GenerationVersion)
        {
            return cached.mask;
        }

        bool[,] exteriorBlocked = new bool[width, height];
        Queue<(int x, int y)> queue = new Queue<(int x, int y)>();

        for (int x = 0; x < width; x++)
        {
            EnqueueExteriorBlockedCell(x, 0, mapGenerator, exteriorBlocked, queue);
            EnqueueExteriorBlockedCell(x, height - 1, mapGenerator, exteriorBlocked, queue);
        }

        for (int y = 0; y < height; y++)
        {
            EnqueueExteriorBlockedCell(0, y, mapGenerator, exteriorBlocked, queue);
            EnqueueExteriorBlockedCell(width - 1, y, mapGenerator, exteriorBlocked, queue);
        }

        while (queue.Count > 0)
        {
            (int cx, int cy) = queue.Dequeue();

            EnqueueExteriorBlockedCell(cx + 1, cy, mapGenerator, exteriorBlocked, queue);
            EnqueueExteriorBlockedCell(cx - 1, cy, mapGenerator, exteriorBlocked, queue);
            EnqueueExteriorBlockedCell(cx, cy + 1, mapGenerator, exteriorBlocked, queue);
            EnqueueExteriorBlockedCell(cx, cy - 1, mapGenerator, exteriorBlocked, queue);
        }

        bool[,] interiorHoles = new bool[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                interiorHoles[x, y] = !mapGenerator.GetIsPlayable(x, y) && !exteriorBlocked[x, y];
            }
        }

        MaskCacheByGenerator[mapGenerator] = new CacheEntry
        {
            generationVersion = mapGenerator.GenerationVersion,
            mask = interiorHoles
        };

        return interiorHoles;
    }

    public static void InvalidateCache(MapGenerator mapGenerator)
    {
        if (mapGenerator == null)
            return;

        MaskCacheByGenerator.Remove(mapGenerator);
    }

    private static void EnqueueExteriorBlockedCell(int x, int y, MapGenerator mapGenerator, bool[,] exteriorBlocked, Queue<(int x, int y)> queue)
    {
        int width = mapGenerator.width;
        int height = mapGenerator.height;

        if (x < 0 || x >= width || y < 0 || y >= height)
            return;

        if (mapGenerator.GetIsPlayable(x, y) || exteriorBlocked[x, y])
            return;

        exteriorBlocked[x, y] = true;
        queue.Enqueue((x, y));
    }
}