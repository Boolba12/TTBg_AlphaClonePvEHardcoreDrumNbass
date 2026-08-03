#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class ItemPreviewGalleryWindow : EditorWindow
{
    public const string CatalogPath =
        "Assets/UI/Presentation/DevelopmentItemPresentationCatalog.asset";

    private Vector2 scroll;
    private ItemPresentationCatalog catalog;

    [MenuItem("Tools/Purgatory UI/Open Item Preview Gallery")]
    public static void Open()
    {
        ItemPreviewGalleryWindow window = GetWindow<ItemPreviewGalleryWindow>();
        window.titleContent = new GUIContent("Item Preview Gallery");
        window.minSize = new Vector2(520f, 320f);
        window.Show();
    }

    private void OnEnable()
    {
        Reload();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Development-only item presentation", EditorStyles.boldLabel);
            if (GUILayout.Button("Reload", GUILayout.Width(76f)))
                Reload();
        }

        EditorGUILayout.HelpBox(
            "This gallery previews presentation assets only. It does not equip items, " +
            "change ownership, or instantiate models into a battle scene.",
            MessageType.Info);

        if (catalog == null)
        {
            EditorGUILayout.HelpBox(
                $"Catalog not found at {CatalogPath}. Run the explicitly confirmed DEV visual rebuild.",
                MessageType.Warning);
            return;
        }

        if (catalog.Entries.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No classified item presentation records are available. The empty state is valid.",
                MessageType.None);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (ItemPresentationRecord record in catalog.Entries)
            DrawRecord(record);
        EditorGUILayout.EndScrollView();
    }

    private static void DrawRecord(ItemPresentationRecord record)
    {
        if (record == null)
            return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        using (new EditorGUILayout.HorizontalScope())
        {
            Rect previewRect = GUILayoutUtility.GetRect(132f, 132f, GUILayout.Width(132f));
            Texture previewTexture = record.PreviewSprite != null
                ? AssetPreview.GetAssetPreview(record.PreviewSprite)
                : record.ModelPrefab != null ? AssetPreview.GetAssetPreview(record.ModelPrefab) : null;
            if (previewTexture != null)
                GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit, true);
            else
                EditorGUI.HelpBox(previewRect, "Preview unavailable", MessageType.None);

            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField(record.DisplayName, EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Stable ID", record.StableId);
                EditorGUILayout.LabelField("Category", record.Category.ToString());
                EditorGUILayout.LabelField("Placeholder", record.IsPlaceholder ? "Yes" : "No");
                EditorGUILayout.LabelField(
                    record.DevelopmentDescription ?? string.Empty,
                    EditorStyles.wordWrappedLabel,
                    GUILayout.MinHeight(42f));

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(record.PreviewSprite == null))
                    {
                        if (GUILayout.Button("Ping preview"))
                            EditorGUIUtility.PingObject(record.PreviewSprite);
                    }
                    using (new EditorGUI.DisabledScope(record.ModelPrefab == null))
                    {
                        if (GUILayout.Button("Ping model"))
                            EditorGUIUtility.PingObject(record.ModelPrefab);
                    }
                }
            }
        }
    }

    private void Reload()
    {
        catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(CatalogPath);
        Repaint();
    }
}
#endif
