using UnityEngine;
using UnityEngine.UI;

/// <summary>One static UI mesh for the whole generated grid; no per-cell GameObjects.</summary>
public sealed class MinimapGridGraphic : MaskableGraphic
{
    [SerializeField] private Color playableColor = new Color32(57, 66, 62, 255);
    [SerializeField] private Color blockedColor = new Color32(16, 19, 20, 225);
    [SerializeField, Range(0f, 0.25f)] private float cellInset = 0.035f;

    private MapGenerator mapGenerator;

    public int PotentialElementCount { get; private set; }
    public int PlayableElementCount { get; private set; }
    public int BuildCount { get; private set; }

    public void Configure(MapGenerator generator, PurgatoryUITheme theme)
    {
        mapGenerator = generator;
        PotentialElementCount = generator != null ? generator.PotentialCellCount : 0;
        PlayableElementCount = generator != null ? generator.PlayableCellCount : 0;
        if (theme != null)
        {
            playableColor = Color.Lerp(theme.DarkSteel, theme.Granite, 0.45f);
            blockedColor = Color.Lerp(theme.BlackStone, theme.DarkSteel, 0.15f);
        }
        BuildCount++;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (mapGenerator == null || !mapGenerator.HasGeneratedData)
        {
            PotentialElementCount = 0;
            PlayableElementCount = 0;
            return;
        }

        PotentialElementCount = 0;
        PlayableElementCount = 0;

        Rect area = rectTransform.rect;
        float cellWidth = area.width / mapGenerator.Width;
        float cellHeight = area.height / mapGenerator.Height;
        float insetX = cellWidth * cellInset;
        float insetY = cellHeight * cellInset;
        for (int y = 0; y < mapGenerator.Height; y++)
        {
            for (int x = 0; x < mapGenerator.Width; x++)
            {
                bool playable = mapGenerator.GetIsPlayable(x, y);
                Color32 cellColor = playable ? playableColor : blockedColor;
                float left = area.xMin + x * cellWidth + insetX;
                float bottom = area.yMin + y * cellHeight + insetY;
                AddQuad(
                    vertexHelper,
                    new Rect(left, bottom, cellWidth - insetX * 2f, cellHeight - insetY * 2f),
                    cellColor);
                PotentialElementCount++;
                if (playable)
                    PlayableElementCount++;
            }
        }
    }

    private static void AddQuad(VertexHelper helper, Rect rect, Color32 color)
    {
        int start = helper.currentVertCount;
        helper.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
        helper.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
        helper.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
        helper.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
        helper.AddTriangle(start, start + 1, start + 2);
        helper.AddTriangle(start + 2, start + 3, start);
    }
}
