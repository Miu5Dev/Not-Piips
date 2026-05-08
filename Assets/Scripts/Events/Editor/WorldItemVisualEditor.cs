using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(WorldItemVisual))]
public class WorldItemVisualEditor : Editor
{
    itemSO    _previewItem;
    int       _previewAmount = 1;
    Texture2D _checkerTex;

    SerializedProperty _bobHeight;
    SerializedProperty _bobSpeed;
    SerializedProperty _groundOffset;
    SerializedProperty _fitPadding;
    SerializedProperty _dropUpForce;
    SerializedProperty _dropSpinTorque;
    SerializedProperty _useDropPhysics;
    SerializedProperty _faceCamera;
    SerializedProperty _groundLayers;
    SerializedProperty _weaponColor;
    SerializedProperty _ammoColor;
    SerializedProperty _healthColor;
    SerializedProperty _shieldColor;
    SerializedProperty _defaultColor;

    void OnEnable()
    {
        _bobHeight      = serializedObject.FindProperty("bobHeight");
        _bobSpeed       = serializedObject.FindProperty("bobSpeed");
        _groundOffset   = serializedObject.FindProperty("groundOffset");
        _fitPadding     = serializedObject.FindProperty("fitPadding");
        _dropUpForce    = serializedObject.FindProperty("dropUpForce");
        _dropSpinTorque = serializedObject.FindProperty("dropSpinTorque");
        _useDropPhysics = serializedObject.FindProperty("useDropPhysics");
        _faceCamera     = serializedObject.FindProperty("faceCamera");
        _groundLayers   = serializedObject.FindProperty("groundLayers");
        _weaponColor    = serializedObject.FindProperty("weaponColor");
        _ammoColor      = serializedObject.FindProperty("ammoColor");
        _healthColor    = serializedObject.FindProperty("healthColor");
        _shieldColor    = serializedObject.FindProperty("shieldColor");
        _defaultColor   = serializedObject.FindProperty("defaultColor");
    }

    // ── Inspector ─────────────────────────────────────────────────────────
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Item Preview", EditorStyles.boldLabel);
        _previewItem   = (itemSO)EditorGUILayout.ObjectField("Item SO", _previewItem, typeof(itemSO), false);
        _previewAmount = EditorGUILayout.IntSlider("Amount", _previewAmount, 1, 99);

        if (_previewItem != null && _previewItem.icon != null)
            DrawSpritePreview(_previewItem);
        else
            DrawEmptyPreview();

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Levitation", EditorStyles.boldLabel);
        EditorGUILayout.Slider(_bobHeight,    0f,   0.5f, "Bob Height");
        EditorGUILayout.Slider(_bobSpeed,     0.1f, 5f,   "Bob Speed");
        EditorGUILayout.Slider(_groundOffset, 0f,   2f,   "Ground Offset");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Drop Physics", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_useDropPhysics, new GUIContent("Use Drop Physics"));
        if (_useDropPhysics.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Slider(_dropUpForce,    0f, 10f, "Up Force");
            EditorGUILayout.Slider(_dropSpinTorque, 0f, 10f, "Spin Torque");
            EditorGUILayout.PropertyField(_groundLayers, new GUIContent("Ground Layers"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Visual", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_faceCamera);
        EditorGUILayout.Slider(_fitPadding, 0.1f, 2f, "Fit Padding");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Item Colors", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_weaponColor,  new GUIContent("Weapon"));
        EditorGUILayout.PropertyField(_ammoColor,    new GUIContent("Ammo"));
        EditorGUILayout.PropertyField(_healthColor,  new GUIContent("Health"));
        EditorGUILayout.PropertyField(_shieldColor,  new GUIContent("Shield"));
        EditorGUILayout.PropertyField(_defaultColor, new GUIContent("Default"));

        serializedObject.ApplyModifiedProperties();

        // Forzar repaint de la Scene View cuando cambia algo
        if (GUI.changed)
            SceneView.RepaintAll();
    }

    // ── Scene View Gizmos ─────────────────────────────────────────────────
    void OnSceneGUI()
    {
        var visual   = (WorldItemVisual)target;
        var t        = visual.transform;

        float groundOffset = _groundOffset.floatValue;
        float bobHeight    = _bobHeight.floatValue;
        float fitPadding   = _fitPadding.floatValue;

        // Posición de reposo del item (con groundOffset)
        Vector3 restPos = t.position + Vector3.up * groundOffset;

        // ── Dibuja el sprite en la scene si hay item asignado ─────────────
        if (_previewItem != null && _previewItem.icon != null)
        {
            DrawSceneSprite(_previewItem, restPos, fitPadding, visual);
            DrawSceneLabel(_previewItem, restPos, _previewAmount, visual);
        }

        // ── Gizmo: línea de bob (rango de levitación) ─────────────────────
        Handles.color = new Color(0.4f, 0.9f, 1f, 0.8f);
        Handles.DrawDottedLine(
            restPos - Vector3.up * bobHeight,
            restPos + Vector3.up * bobHeight,
            3f
        );

        // ── Gizmo: punto de reposo ────────────────────────────────────────
        Handles.color = Color.cyan;
        Handles.SphereHandleCap(0, restPos, Quaternion.identity, 0.04f, EventType.Repaint);

        // ── Gizmo: línea del suelo al punto de reposo ─────────────────────
        Handles.color = new Color(1f, 1f, 1f, 0.2f);
        Handles.DrawDottedLine(t.position, restPos, 4f);

        // ── Gizmo: arco de bob (visual del rango) ────────────────────────
        Handles.color = new Color(0.4f, 0.9f, 1f, 0.15f);
        Handles.DrawWireDisc(restPos, Vector3.up, 0.1f);

        // ── Drop physics: arco de trayectoria estimada ────────────────────
        if (_useDropPhysics.boolValue)
        {
            float upForce = _dropUpForce.floatValue;
            DrawDropArc(t.position, upForce);
        }
    }

    void DrawSceneSprite(itemSO item, Vector3 worldPos, float fitPadding, WorldItemVisual visual)
    {
        Texture2D tex      = item.icon.texture;
        Rect      texCoords = item.icon.textureRect;

        // Tamaño basado en collider (aproximado desde el editor)
        var col = visual.GetComponent<Collider>();
        float colSize = col != null ? Mathf.Min(col.bounds.size.x, col.bounds.size.y, col.bounds.size.z) : 1f;
        float size    = colSize * fitPadding;

        // Calcular aspect ratio del sprite
        float aspect = texCoords.width / texCoords.height;
        float w = aspect >= 1f ? size : size * aspect;
        float h = aspect >= 1f ? size / aspect : size;

        // 4 vértices del quad (billboard mirando a la scene camera)
        SceneView sv = SceneView.currentDrawingSceneView;
        if (sv == null) return;

        Vector3 camRight = sv.camera.transform.right;
        Vector3 camUp    = sv.camera.transform.up;

        Vector3 bl = worldPos - camRight * (w * 0.5f) - camUp * (h * 0.5f);
        Vector3 br = worldPos + camRight * (w * 0.5f) - camUp * (h * 0.5f);
        Vector3 tl = worldPos - camRight * (w * 0.5f) + camUp * (h * 0.5f);
        Vector3 tr = worldPos + camRight * (w * 0.5f) + camUp * (h * 0.5f);

        // UV del sprite dentro del atlas
        Rect uv = new Rect(
            texCoords.x      / tex.width,
            texCoords.y      / tex.height,
            texCoords.width  / tex.width,
            texCoords.height / tex.height
        );

        // Dibujar con Handles.DrawTexture (requiere Unity 2021+)
        Color itemColor = GetSceneItemColor(item, visual);
        Handles.color   = Color.white;

        // Usamos un material GUI temporal para renderizar el sprite
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = tex;
        mat.color = Color.white;
        mat.SetPass(0);

        GL.PushMatrix();
        GL.Begin(GL.QUADS);
        GL.Color(Color.white);
        GL.TexCoord2(uv.x,          uv.y);          GL.Vertex(bl);
        GL.TexCoord2(uv.x + uv.width, uv.y);        GL.Vertex(br);
        GL.TexCoord2(uv.x + uv.width, uv.y + uv.height); GL.Vertex(tr);
        GL.TexCoord2(uv.x,          uv.y + uv.height); GL.Vertex(tl);
        GL.End();
        GL.PopMatrix();

        DestroyImmediate(mat);
    }

    void DrawSceneLabel(itemSO item, Vector3 worldPos, int amount, WorldItemVisual visual)
    {
        var col      = visual.GetComponent<Collider>();
        float colSize = col != null ? Mathf.Min(col.bounds.size.x, col.bounds.size.y, col.bounds.size.z) : 1f;
        float offset  = colSize * 0.7f;

        Color  labelColor = GetSceneItemColor(item, visual);
        string label      = amount > 1 ? $"{item.name}  x{amount}" : item.name;

        GUIStyle style = new GUIStyle
        {
            normal    = { textColor = labelColor },
            fontSize  = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        Handles.Label(worldPos + Vector3.up * offset, label, style);
    }

    void DrawDropArc(Vector3 startPos, float upForce)
    {
        // Simula la trayectoria parabolica estimada
        const int   steps    = 30;
        const float dt       = 0.08f;
        float       vy       = upForce;
        Vector3     pos      = startPos;

        Handles.color = new Color(1f, 0.6f, 0.1f, 0.6f);

        for (int i = 0; i < steps; i++)
        {
            vy -= 9.81f * dt;
            Vector3 next = pos + Vector3.up * (vy * dt);
            if (next.y < startPos.y - 0.2f) break; // para aproximadamente al suelo
            Handles.DrawLine(pos, next);
            pos = next;
        }

        // Flecha indicando dirección inicial
        Handles.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Handles.ArrowHandleCap(0,
            startPos,
            Quaternion.LookRotation(Vector3.up),
            upForce * 0.15f,
            EventType.Repaint
        );
    }

    Color GetSceneItemColor(itemSO item, WorldItemVisual visual)
    {
        if (item is WeaponSO) return visual.weaponColor;
        if (item is AmmoSO)   return visual.ammoColor;
        if (item is HealthSO h)
            return h.healthType == HealthType.Health ? visual.healthColor : visual.shieldColor;
        return visual.defaultColor;
    }

    // ── Inspector Preview ─────────────────────────────────────────────────
    void DrawSpritePreview(itemSO item)
    {
        if (_checkerTex == null) _checkerTex = BuildCheckerTexture(128, 8);

        Rect previewRect = GUILayoutUtility.GetRect(0, 120,
            GUILayout.ExpandWidth(true), GUILayout.Height(120));

        GUI.DrawTexture(previewRect, _checkerTex, ScaleMode.ScaleAndCrop);

        float     padding    = 10f;
        Rect      spriteRect = new Rect(previewRect.x + padding, previewRect.y + padding,
                                        previewRect.width - padding * 2f, previewRect.height - padding * 2f);
        Texture2D spriteTex  = item.icon.texture;
        Rect      texCoords  = item.icon.textureRect;
        Rect      uvRect     = new Rect(
            texCoords.x      / spriteTex.width,
            texCoords.y      / spriteTex.height,
            texCoords.width  / spriteTex.width,
            texCoords.height / spriteTex.height
        );

        float aspect     = texCoords.width / texCoords.height;
        float rectAspect = spriteRect.width / spriteRect.height;
        if (aspect > rectAspect)
            spriteRect = new Rect(spriteRect.x,
                spriteRect.y + (spriteRect.height - spriteRect.width / aspect) * 0.5f,
                spriteRect.width, spriteRect.width / aspect);
        else
            spriteRect = new Rect(
                spriteRect.x + (spriteRect.width - spriteRect.height * aspect) * 0.5f,
                spriteRect.y, spriteRect.height * aspect, spriteRect.height);

        GUI.DrawTextureWithTexCoords(spriteRect, spriteTex, uvRect, true);

        var style = new GUIStyle(EditorStyles.boldLabel)
        {
            normal    = { textColor = GetSceneItemColor(item, (WorldItemVisual)target) },
            fontSize  = 11,
            alignment = TextAnchor.LowerCenter
        };
        string label = _previewAmount > 1 ? $"{item.name}  x{_previewAmount}" : item.name;
        GUI.Label(new Rect(previewRect.x, previewRect.yMax - 22f, previewRect.width, 20f), label, style);
    }

    void DrawEmptyPreview()
    {
        if (_checkerTex == null) _checkerTex = BuildCheckerTexture(128, 8);
        Rect rect = GUILayoutUtility.GetRect(0, 120, GUILayout.ExpandWidth(true), GUILayout.Height(120));
        GUI.DrawTexture(rect, _checkerTex, ScaleMode.ScaleAndCrop);
        GUI.Label(rect, "Assign an Item SO to preview",
            new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 11 });
    }

    static Texture2D BuildCheckerTexture(int size, int squares)
    {
        var tex    = new Texture2D(size, size);
        int sqSize = size / squares;
        Color dark  = new Color(0.18f, 0.18f, 0.18f);
        Color light = new Color(0.25f, 0.25f, 0.25f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, ((x / sqSize + y / sqSize) % 2 == 0) ? dark : light);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }
}