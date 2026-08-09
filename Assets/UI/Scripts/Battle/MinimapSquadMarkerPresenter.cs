using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class MinimapSquadMarkerPresenter : MonoBehaviour
{
    private sealed class MarkerBinding
    {
        public SquadBattleController Controller;
        public Image Image;
        public Action<Vector2Int> CellHandler;
        public Action DefeatHandler;
    }

    [SerializeField] private RectTransform markerLayer;
    [SerializeField] private PurgatoryUITheme theme;
    [SerializeField, Min(4f)] private float markerSize = 12f;

    private readonly List<MarkerBinding> bindings = new List<MarkerBinding>();
    private MinimapCoordinateMapper mapper;

    public int MarkerCount => bindings.Count;

    public void Configure(RectTransform layer, PurgatoryUITheme configuredTheme)
    {
        markerLayer = layer;
        theme = configuredTheme;
    }

    public bool Bind(
        IReadOnlyList<SquadBattleController> controllers,
        MinimapCoordinateMapper coordinateMapper)
    {
        Unbind();
        if (markerLayer == null || coordinateMapper == null || controllers == null)
            return false;
        mapper = coordinateMapper;
        for (int i = 0; i < controllers.Count; i++)
        {
            SquadBattleController controller = controllers[i];
            if (controller == null || !controller.IsInitialized || controller.GridAnchor == null)
                continue;
            CreateMarker(controller);
        }
        return bindings.Count > 0;
    }

    public RectTransform GetMarkerRect(string squadId)
    {
        MarkerBinding binding = bindings.Find(item => item.Controller.SquadId == squadId);
        return binding?.Image != null ? binding.Image.rectTransform : null;
    }

    public bool DisplaysDefeated(string squadId)
    {
        MarkerBinding binding = bindings.Find(item => item.Controller.SquadId == squadId);
        return binding != null && binding.Controller.Runtime.State.IsDefeated;
    }

    private void CreateMarker(SquadBattleController controller)
    {
        GameObject markerObject = new GameObject(
            $"SquadMarker_{bindings.Count}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        markerObject.transform.SetParent(markerLayer, false);
        Image marker = markerObject.GetComponent<Image>();
        marker.raycastTarget = false;
        marker.color = GetSideColor(controller.Side);
        RectTransform rect = marker.rectTransform;
        rect.sizeDelta = Vector2.one * markerSize;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchorMin = rect.anchorMax = mapper.GridToNormalized(controller.GridAnchor.CurrentCell);
        rect.anchoredPosition = Vector2.zero;

        MarkerBinding binding = new MarkerBinding
        {
            Controller = controller,
            Image = marker
        };
        binding.CellHandler = cell => UpdatePosition(binding, cell);
        binding.DefeatHandler = () => UpdateDefeated(binding);
        controller.GridAnchor.CellChanged += binding.CellHandler;
        controller.Runtime.OnSquadDefeated += binding.DefeatHandler;
        bindings.Add(binding);
        if (controller.Runtime.State.IsDefeated)
            UpdateDefeated(binding);
    }

    private Color GetSideColor(BattleSide side)
    {
        if (theme != null)
            return side == BattleSide.Player ? theme.PlayerSide : theme.EnemySide;
        return side == BattleSide.Player
            ? new Color32(52, 160, 108, 255)
            : new Color32(174, 69, 58, 255);
    }

    private void UpdatePosition(MarkerBinding binding, Vector2Int cell)
    {
        if (binding?.Image == null || mapper == null)
            return;
        Vector2 normalized = mapper.GridToNormalized(cell);
        binding.Image.rectTransform.anchorMin = normalized;
        binding.Image.rectTransform.anchorMax = normalized;
        binding.Image.rectTransform.anchoredPosition = Vector2.zero;
    }

    private void UpdateDefeated(MarkerBinding binding)
    {
        if (binding?.Image == null)
            return;
        binding.Image.color = theme != null ? theme.Disabled : Color.gray;
        binding.Image.rectTransform.sizeDelta = Vector2.one * markerSize * 0.75f;
    }

    private void OnDestroy() => Unbind();

    public void Unbind()
    {
        foreach (MarkerBinding binding in bindings)
        {
            if (binding.Controller != null)
            {
                if (binding.Controller.GridAnchor != null)
                    binding.Controller.GridAnchor.CellChanged -= binding.CellHandler;
                if (binding.Controller.Runtime != null)
                    binding.Controller.Runtime.OnSquadDefeated -= binding.DefeatHandler;
            }
            if (binding.Image != null)
            {
                if (Application.isPlaying)
                    Destroy(binding.Image.gameObject);
                else
                    DestroyImmediate(binding.Image.gameObject);
            }
        }
        bindings.Clear();
        mapper = null;
    }
}
