using UnityEngine;
using UnityEditor;

/// <summary>
/// Runs once on editor load. Inserts the "Minimap" layer into TagManager if it doesn't exist.
/// </summary>
[InitializeOnLoad]
public static class MinimapLayerSetup
{
    static MinimapLayerSetup() => EnsureLayer("Minimap");

    private static void EnsureLayer(string name)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));

        var layers = tagManager.FindProperty("layers");

        for (int i = 0; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == name) return;

        for (int i = 8; i < 32 && i < layers.arraySize; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;

            slot.stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Minimap] Layer '{name}' added at index {i}. Reopen any open scenes to apply culling mask changes.");
            return;
        }

        Debug.LogError($"[Minimap] No free layer slot for '{name}'. Add it manually: Project Settings → Tags and Layers.");
    }
}
