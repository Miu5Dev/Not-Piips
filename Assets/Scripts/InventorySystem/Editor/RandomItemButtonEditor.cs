using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(RandomItemButton))]
public class RandomItemButtonEditor : Editor
{
    SerializedProperty _possibleItems;

    void OnEnable()
    {
        _possibleItems = serializedObject.FindProperty("possibleItems");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Calcular total de weights para los porcentajes
        float total = 0f;
        for (int i = 0; i < _possibleItems.arraySize; i++)
        {
            var entry  = _possibleItems.GetArrayElementAtIndex(i);
            total += entry.FindPropertyRelative("weight").floatValue;
        }

        EditorGUILayout.LabelField("Possible Items", EditorStyles.boldLabel);

        for (int i = 0; i < _possibleItems.arraySize; i++)
        {
            var entry     = _possibleItems.GetArrayElementAtIndex(i);
            var item      = entry.FindPropertyRelative("item");
            var minAmount = entry.FindPropertyRelative("minAmount");
            var maxAmount = entry.FindPropertyRelative("maxAmount");
            var weight    = entry.FindPropertyRelative("weight");

            float percent = total > 0f ? (weight.floatValue / total) * 100f : 0f;

            // Caja por entry
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header con nombre del item y porcentaje
            string itemName = item.objectReferenceValue != null
                ? item.objectReferenceValue.name
                : "Empty";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"[{i}]  {itemName}", EditorStyles.boldLabel);

            // Badge de porcentaje coloreado
            var style = new GUIStyle(EditorStyles.label)
            {
                fontStyle  = FontStyle.Bold,
                alignment  = TextAnchor.MiddleRight,
                normal     = { textColor = PercentColor(percent) }
            };
            EditorGUILayout.LabelField($"{percent:F1}%", style, GUILayout.Width(55));
            EditorGUILayout.EndHorizontal();

            // Barra de probabilidad
            var barRect = GUILayoutUtility.GetRect(0, 6, GUILayout.ExpandWidth(true));
            barRect.x     += 2; barRect.width -= 4;
            EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));
            var fillRect  = new Rect(barRect.x, barRect.y, barRect.width * (percent / 100f), barRect.height);
            EditorGUI.DrawRect(fillRect, PercentColor(percent));

            GUILayout.Space(4);

            EditorGUILayout.PropertyField(item,      new GUIContent("Item"));
            EditorGUILayout.PropertyField(minAmount, new GUIContent("Min Amount"));
            EditorGUILayout.PropertyField(maxAmount, new GUIContent("Max Amount"));
            EditorGUILayout.PropertyField(weight,    new GUIContent("Weight"));

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
                _possibleItems.DeleteArrayElementAtIndex(i);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            GUILayout.Space(2);
        }

        GUILayout.Space(4);
        if (GUILayout.Button("+ Add Entry"))
            _possibleItems.InsertArrayElementAtIndex(_possibleItems.arraySize);

        serializedObject.ApplyModifiedProperties();
    }

    // Verde si alta prob, amarillo si media, rojo si baja
    Color PercentColor(float percent)
    {
        if (percent >= 50f) return new Color(0.3f, 0.85f, 0.4f);
        if (percent >= 20f) return new Color(0.95f, 0.75f, 0.2f);
        return new Color(0.9f, 0.35f, 0.35f);
    }
}