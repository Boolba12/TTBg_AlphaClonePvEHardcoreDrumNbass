using UnityEngine;
using UnityEngine.UI;

public class GameStartUI : MonoBehaviour
{
    [Header("UI")]
    public InputField seedInputField;
    public Button startButton;
    public GameObject menuRoot;

    [Header("Map")]
    public MapGenerator mapGenerator;
    public MapRenderer mapRenderer;
    public MapRockPlacer mapRockPlacer;
    public PlayerController playerController;
    public EnemyController enemyController;

    private void Awake()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGame);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }
    }

    public void StartGame()
    {
        if (mapGenerator == null)
        {
            Debug.LogError("GameStartUI: MapGenerator is not assigned.");
            return;
        }

        if (mapRenderer == null)
        {
            Debug.LogError("GameStartUI: MapRenderer is not assigned.");
            return;
        }

        if (seedInputField != null)
        {
            string seedText = seedInputField.text.Trim();
            if (!string.IsNullOrEmpty(seedText))
            {
                if (int.TryParse(seedText, out int parsedSeed))
                {
                    mapGenerator.seed = parsedSeed;
                }
                else
                {
                    Debug.LogWarning($"GameStartUI: Invalid seed '{seedText}', using current seed {mapGenerator.seed}.");
                }
            }
        }

        mapGenerator.Generate();
        mapRenderer.RenderMap();

        if (playerController != null)
            playerController.SetMapReferences(mapGenerator, mapRenderer);

        if (enemyController != null)
            enemyController.SetMapReferences(mapGenerator, mapRenderer, playerController);

        if (mapRockPlacer != null)
            mapRockPlacer.PlaceEnvironment();

        if (menuRoot != null)
        {
            if (menuRoot == mapGenerator.gameObject || menuRoot == mapRenderer.gameObject || menuRoot == gameObject)
            {
                Debug.LogWarning("GameStartUI: menuRoot is pointing at the gameplay object. Assign only the menu panel or menu UI root.");
                return;
            }

            menuRoot.SetActive(false);
        }
    }
}
