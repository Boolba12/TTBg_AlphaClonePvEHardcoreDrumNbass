using UnityEngine;

public sealed class SquadSelectionView : MonoBehaviour
{
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField] private LineRenderer selectionRing;
    [SerializeField, Range(12, 64)] private int segmentCount = 32;
    [SerializeField, Min(0.1f)] private float radius = 0.47f;
    [SerializeField, Min(0.005f)] private float width = 0.035f;
    [SerializeField] private float height = 0.035f;

    private Material runtimeMaterial;

    public bool IsSelected { get; private set; }

    public void Configure(PurgatoryUITheme configuredTheme, LineRenderer ring)
    {
        theme = configuredTheme;
        selectionRing = ring;
        ConfigureRing();
        SetSelected(false);
    }

    private void Awake()
    {
        ConfigureRing();
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (selectionRing != null)
            selectionRing.enabled = selected;
    }

    private void ConfigureRing()
    {
        if (selectionRing == null)
            selectionRing = GetComponent<LineRenderer>();
        if (selectionRing == null)
            return;

        Shader shader = Shader.Find("Sprites/Default");
        if (runtimeMaterial == null && shader != null)
        {
            runtimeMaterial = new Material(shader) { name = "Runtime_SquadSelectionRing" };
            selectionRing.sharedMaterial = runtimeMaterial;
        }

        Color color = theme != null ? theme.Emerald : new Color32(38, 174, 115, 255);
        selectionRing.useWorldSpace = false;
        selectionRing.loop = true;
        selectionRing.startWidth = width;
        selectionRing.endWidth = width;
        selectionRing.startColor = color;
        selectionRing.endColor = color;
        selectionRing.positionCount = segmentCount;
        for (int i = 0; i < segmentCount; i++)
        {
            float angle = Mathf.PI * 2f * i / segmentCount;
            selectionRing.SetPosition(
                i,
                new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
            return;
        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);
    }
}
