using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MapGenerator))]
public class MapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MapGenerator generator = (MapGenerator)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Generated Info", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Playable cells generated", generator.generatedPlayableCount.ToString());

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }
    }
}
