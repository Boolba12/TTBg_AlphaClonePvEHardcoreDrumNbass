using System;
using UnityEngine;

/// <summary>
/// Central dimension-aware mapping contract shared by every minimap layer.
/// Grid coordinates are represented by cell centres in normalized UI space.
/// </summary>
public sealed class MinimapCoordinateMapper
{
    private readonly Func<int, int, bool> isPlayable;
    private readonly Func<Vector2Int, Vector3> cellWorldCenter;

    public int Width { get; }
    public int Height { get; }
    public Bounds WorldBounds { get; }
    public float MapAspect => Height > 0 ? Width / (float)Height : 1f;

    public MinimapCoordinateMapper(
        int width,
        int height,
        Bounds worldBounds,
        Func<int, int, bool> playableLookup,
        Func<Vector2Int, Vector3> worldCenterLookup)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
        WorldBounds = worldBounds;
        isPlayable = playableLookup ?? throw new ArgumentNullException(nameof(playableLookup));
        cellWorldCenter = worldCenterLookup ?? throw new ArgumentNullException(nameof(worldCenterLookup));
    }

    public Vector2 GridToNormalized(Vector2Int cell) => new Vector2(
        (cell.x + 0.5f) / Width,
        (cell.y + 0.5f) / Height);

    public bool TryNormalizedToGrid(
        Vector2 normalized,
        out Vector2Int cell,
        bool requirePlayable = true)
    {
        cell = default;
        if (normalized.x < 0f || normalized.x > 1f ||
            normalized.y < 0f || normalized.y > 1f)
        {
            return false;
        }

        int x = Mathf.Min(Width - 1, Mathf.FloorToInt(normalized.x * Width));
        int y = Mathf.Min(Height - 1, Mathf.FloorToInt(normalized.y * Height));
        cell = new Vector2Int(x, y);
        return !requirePlayable || isPlayable(x, y);
    }

    public Vector2 WorldToNormalized(Vector3 world)
    {
        float xSize = Mathf.Max(0.0001f, WorldBounds.size.x);
        float zSize = Mathf.Max(0.0001f, WorldBounds.size.z);
        return new Vector2(
            Mathf.InverseLerp(WorldBounds.min.x, WorldBounds.min.x + xSize, world.x),
            Mathf.InverseLerp(WorldBounds.min.z, WorldBounds.min.z + zSize, world.z));
    }

    public bool TryNormalizedToWorld(
        Vector2 normalized,
        out Vector3 world,
        bool requirePlayable = true)
    {
        world = default;
        if (!TryNormalizedToGrid(normalized, out Vector2Int cell, requirePlayable))
            return false;
        world = cellWorldCenter(cell);
        return true;
    }

    public static Rect CalculateAspectFitRect(Rect available, float mapAspect)
    {
        if (available.width <= 0f || available.height <= 0f || mapAspect <= 0f)
            return available;
        float availableAspect = available.width / available.height;
        if (availableAspect > mapAspect)
        {
            float width = available.height * mapAspect;
            return new Rect(
                available.x + (available.width - width) * 0.5f,
                available.y,
                width,
                available.height);
        }

        float height = available.width / mapAspect;
        return new Rect(
            available.x,
            available.y + (available.height - height) * 0.5f,
            available.width,
            height);
    }
}
