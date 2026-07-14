using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

public class DebugHUD : MonoBehaviour
{
    [Header("References (optional)")]
    public MapGenerator mapGenerator;
    public MapRenderer mapRenderer;
    public PlayerController playerController;
    public EnemyController enemyController;
    public TurnSystem turnSystem;
    public CameraFollow cameraFollow;

    [Header("Display")]
    public bool visible = true;
    public Vector2 anchor = new Vector2(12f, 12f);
    public Vector2 size = new Vector2(360f, 250f);

    private readonly StringBuilder sb = new StringBuilder(512);
    private float smoothedFps;

    private void Awake()
    {
        AutoBindMissingReferences();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            visible = !visible;

        float dt = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        float currentFps = 1f / dt;
        smoothedFps = Mathf.Lerp(smoothedFps <= 0f ? currentFps : smoothedFps, currentFps, 0.08f);

        if (mapGenerator == null || mapRenderer == null || playerController == null || turnSystem == null)
            AutoBindMissingReferences();
    }

    private void OnGUI()
    {
        if (!visible)
            return;

        GUI.depth = -1000;

        Rect box = new Rect(anchor.x, anchor.y, size.x, size.y);
        GUI.Box(box, "Debug HUD");

        sb.Clear();
        sb.AppendLine($"FPS: {smoothedFps:0.0}");
        sb.AppendLine($"Time: {Time.time:0.00}s");

        if (mapGenerator != null)
        {
            sb.AppendLine($"Seed: {mapGenerator.seed}");
            sb.AppendLine($"Map: {mapGenerator.width} x {mapGenerator.height}");
            sb.AppendLine($"Playable: {mapGenerator.generatedPlayableCount}/{mapGenerator.playableCount}");
            Vector2Int start = mapGenerator.GetStartCell();
            sb.AppendLine($"Start Cell: ({start.x}, {start.y})");
            sb.AppendLine($"Generation: {mapGenerator.GenerationVersion}");
        }
        else
        {
            sb.AppendLine("MapGenerator: missing");
        }

        sb.AppendLine($"Map Rendered: {(mapRenderer != null && mapRenderer.HasMap ? "yes" : "no")}");

        if (playerController != null)
        {
            Vector2Int p = playerController.CurrentCell;
            sb.AppendLine($"Player Cell: ({p.x}, {p.y})");
            sb.AppendLine($"Player Max Steps: {playerController.maxStepsPerTurn}");
        }
        else
        {
            sb.AppendLine("PlayerController: missing");
        }

        if (enemyController != null)
        {
            Vector2Int e = enemyController.CurrentCell;
            sb.AppendLine($"Enemy Cell: ({e.x}, {e.y})");
            sb.AppendLine($"Enemy Max Steps: {enemyController.maxStepsPerTurn}");
        }
        else
        {
            sb.AppendLine("EnemyController: missing");
        }

        if (turnSystem != null)
            sb.AppendLine($"Turn: {turnSystem.CurrentTurnLabel}");
        else
            sb.AppendLine("TurnSystem: missing");

        if (cameraFollow != null)
            sb.AppendLine($"Camera Target: {(cameraFollow.target != null ? cameraFollow.target.name : "none")}");

        Rect content = new Rect(box.x + 10f, box.y + 24f, box.width - 20f, box.height - 34f);
        GUI.Label(content, sb.ToString());
    }

    private void AutoBindMissingReferences()
    {
        if (mapGenerator == null)
            mapGenerator = FindObjectOfType<MapGenerator>();

        if (mapRenderer == null)
            mapRenderer = FindObjectOfType<MapRenderer>();

        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>();

        if (enemyController == null)
            enemyController = FindObjectOfType<EnemyController>();

        if (turnSystem == null)
            turnSystem = FindObjectOfType<TurnSystem>();

        if (cameraFollow == null)
            cameraFollow = FindObjectOfType<CameraFollow>();
    }
}
