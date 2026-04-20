using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
#endif

/// <summary>
/// Visual listener for the EventBus.
/// - If the event has a bool field named "pressed" → shows On Pressed / On Released.
/// - Otherwise → shows a single On Raised callback.
/// </summary>
public class EventBusListener : MonoBehaviour
{
    [HideInInspector] public string selectedEventTypeName = "";

    [Tooltip("Invoked when the event is raised (notification events) or pressed = true (button events)")]
    public UnityEvent onRaised;

    [Tooltip("Invoked when pressed = false / released (button events only)")]
    public UnityEvent onReleased;

    [Tooltip("Always calls onRaised regardless of the bool state (button events only)")]
    public bool callOnBothStates = false;

    // =========================================================
    // RUNTIME — type cache (built once)
    // =========================================================
    private static Dictionary<string, Type> _typeCache;

    public static Type ResolveType(string typeName)
    {
        if (_typeCache == null)
        {
            _typeCache = new Dictionary<string, Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in assembly.GetTypes())
                        if (!_typeCache.ContainsKey(t.Name))
                            _typeCache[t.Name] = t;
                }
                catch { }
            }
        }
        return _typeCache.TryGetValue(typeName, out var type) ? type : null;
    }

    /// <summary>
    /// Returns the bool member named "pressed" only.
    /// Any other bool field (e.g. IsHipFiring) does NOT qualify as a button event.
    /// </summary>
    public static MemberInfo GetBoolMember(Type eventType)
    {
        const string PRESSED = "pressed";

        var field = eventType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                             .FirstOrDefault(f => f.FieldType == typeof(bool) &&
                                                  f.Name.Equals(PRESSED, StringComparison.OrdinalIgnoreCase));
        if (field != null) return field;

        return eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(p => p.PropertyType == typeof(bool) &&
                                             p.Name.Equals(PRESSED, StringComparison.OrdinalIgnoreCase));
    }

    public static bool ReadBool(MemberInfo member, object obj)
    {
        if (member is FieldInfo    fi) return (bool)fi.GetValue(obj);
        if (member is PropertyInfo pi) return (bool)pi.GetValue(obj);
        return true;
    }

    // =========================================================
    // DYNAMIC SUBSCRIPTION
    // =========================================================
    private Delegate   _subscribedDelegate;
    private MethodInfo _unsubscribeMethod;

    private void OnEnable()  => Subscribe(selectedEventTypeName);
    private void OnDisable() => Unsubscribe();

    private void Subscribe(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return;

        Type eventType = ResolveType(typeName);
        if (eventType == null)
        {
            Debug.LogWarning($"[EventBusListener] Event type not found at runtime: '{typeName}'");
            return;
        }

        MemberInfo boolMember  = GetBoolMember(eventType);
        bool       isButtonEvent = boolMember != null;

        Action<object> callback = evtObj =>
        {
            if (!isButtonEvent)
            {
                onRaised?.Invoke();
                return;
            }

            bool pressed = ReadBool(boolMember, evtObj);

            if (callOnBothStates) { onRaised?.Invoke(); return; }
            if (pressed) onRaised?.Invoke();
            else         onReleased?.Invoke();
        };

        MethodInfo wrapper = typeof(EventBusListener)
            .GetMethod(nameof(CreateTypedCallback), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(eventType);

        _subscribedDelegate = (Delegate)wrapper.Invoke(null, new object[] { callback });

        _unsubscribeMethod = typeof(EventBus)
            .GetMethod("Unsubscribe", BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(eventType);

        typeof(EventBus)
            .GetMethod("Subscribe", BindingFlags.Public | BindingFlags.Static)
            .MakeGenericMethod(eventType)
            .Invoke(null, new object[] { _subscribedDelegate });
    }

    private static Action<T> CreateTypedCallback<T>(Action<object> inner) => evt => inner(evt);

    private void Unsubscribe()
    {
        if (_subscribedDelegate == null || _unsubscribeMethod == null) return;
        _unsubscribeMethod.Invoke(null, new object[] { _subscribedDelegate });
        _subscribedDelegate = null;
        _unsubscribeMethod  = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        Unsubscribe();
        Subscribe(selectedEventTypeName);
    }
#endif
}


// =========================================================
// SCRIPTABLE OBJECT — shared folder config
// Auto-created at: Assets/Resources/EventBusListenerConfig.asset
// =========================================================
#if UNITY_EDITOR

[CreateAssetMenu(fileName = "EventBusListenerConfig", menuName = "EventBus/Listener Config")]
public class EventBusListenerConfig : ScriptableObject
{
    [Tooltip("Path relative to Assets/ where your event scripts live.\nExample: Scripts/Events")]
    public string eventsFolder = "Scripts/Events";

    private static EventBusListenerConfig _instance;

    public static EventBusListenerConfig Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<EventBusListenerConfig>("EventBusListenerConfig");
            if (_instance != null) return _instance;

            var guids = AssetDatabase.FindAssets("t:EventBusListenerConfig");
            if (guids.Length > 0)
            {
                _instance = AssetDatabase.LoadAssetAtPath<EventBusListenerConfig>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
                if (_instance != null) return _instance;
            }

            return _instance = CreateAndSaveDefault();
        }
    }

    private static EventBusListenerConfig CreateAndSaveDefault()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var asset = CreateInstance<EventBusListenerConfig>();
        AssetDatabase.CreateAsset(asset, "Assets/Resources/EventBusListenerConfig.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EventBusListener] Config auto-created at Assets/Resources/EventBusListenerConfig.asset");
        return asset;
    }

    public static void Invalidate() => _instance = null;
}


// ---- Config custom editor — folder picker lives here ----
[CustomEditor(typeof(EventBusListenerConfig))]
public class EventBusListenerConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var config     = (EventBusListenerConfig)target;
        var folderProp = serializedObject.FindProperty("eventsFolder");

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("⚙  EventBus Listener Config", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Events Folder", EditorStyles.miniBoldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(folderProp.stringValue);
            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("📁 Browse", GUILayout.Width(72)))
            {
                string startPath = Path.Combine(Application.dataPath, folderProp.stringValue);
                if (!Directory.Exists(startPath)) startPath = Application.dataPath;

                string absolute = EditorUtility.OpenFolderPanel("Select Events Folder", startPath, "");

                if (!string.IsNullOrEmpty(absolute) && absolute.StartsWith(Application.dataPath))
                {
                    folderProp.stringValue = absolute.Substring(Application.dataPath.Length).TrimStart('/', '\\');
                    serializedObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                    EventBusListenerEditor.InvalidateCache();
                }
            }
        }

        EditorGUILayout.LabelField($"  Assets/{folderProp.stringValue}", EditorStyles.miniLabel);
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "All EventBusListener components share this config.\n" +
            "Place your event classes (OnXxxEvent) inside the folder above.",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }
}


// =========================================================
// LISTENER CUSTOM EDITOR
// =========================================================
[CustomEditor(typeof(EventBusListener))]
public class EventBusListenerEditor : Editor
{
    private static string   _cachedFolder = null;
    private static string[] _cachedNames  = Array.Empty<string>();
    private static double   _lastScanTime = -1;
    private const  double   COOLDOWN      = 5.0;

    public static void InvalidateCache()
    {
        _cachedFolder = null;
        _cachedNames  = Array.Empty<string>();
        _lastScanTime = -1;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var t            = (EventBusListener)this.target;
        var selectedProp = serializedObject.FindProperty("selectedEventTypeName");
        var raisedProp   = serializedObject.FindProperty("onRaised");
        var releasedProp = serializedObject.FindProperty("onReleased");
        var bothProp     = serializedObject.FindProperty("callOnBothStates");

        // ---- Header ----
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("⚡  EventBus Listener", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // ---- Config link ----
        var config = EventBusListenerConfig.Instance;
        string folder = config != null ? config.eventsFolder : "Scripts/Events";

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Config:", EditorStyles.miniLabel, GUILayout.Width(44));
            if (config != null)
            {
                if (GUILayout.Button("EventBusListenerConfig", EditorStyles.linkLabel))
                {
                    EditorGUIUtility.PingObject(config);
                    Selection.activeObject = config;
                }
            }
            else
            {
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField("Not found — will auto-create on first use", EditorStyles.miniLabel);
                GUI.color = Color.white;
            }
        }

        EditorGUILayout.Space(6);

        // ---- Auto-scan ----
        if (_cachedFolder != folder ||
           (_cachedNames.Length == 0 && (EditorApplication.timeSinceStartup - _lastScanTime) > COOLDOWN))
            RefreshCache(folder);

        // ---- Dropdown ----
        EditorGUILayout.LabelField("Listen To", EditorStyles.miniBoldLabel);

        if (_cachedNames.Length == 0)
        {
            EditorGUILayout.HelpBox(
                $"No OnXxxEvent classes found in:\nAssets/{folder}\n\nCheck the folder in the Config asset.",
                MessageType.Warning);
        }
        else
        {
            int currentIndex = Mathf.Max(0, Array.IndexOf(_cachedNames, selectedProp.stringValue));
            int newIndex     = EditorGUILayout.Popup(currentIndex, _cachedNames);

            if (newIndex != currentIndex || string.IsNullOrEmpty(selectedProp.stringValue))
                selectedProp.stringValue = _cachedNames[newIndex];

            EditorGUILayout.LabelField(
                $"  {_cachedNames.Length} event(s) found in Assets/{folder}",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(6);

        // ---- Detect if button event (has "pressed" field) ----
        string selectedName  = selectedProp.stringValue;
        Type   eventType     = EventBusListener.ResolveType(selectedName);
        bool   isButtonEvent = false;

        if (eventType != null)
        {
            isButtonEvent = EventBusListener.GetBoolMember(eventType) != null;
        }
        else if (!string.IsNullOrEmpty(selectedName))
        {
            // Fallback in Edit Mode: scan source file for "public bool pressed"
            isButtonEvent = SourceHasPressedField(folder, selectedName);
        }

        // ---- Options (button events only) ----
        if (isButtonEvent)
        {
            EditorGUILayout.PropertyField(bothProp, new GUIContent("Call On Both States"));
            EditorGUILayout.Space(6);
        }

        // ---- UnityEvents ----
        if (isButtonEvent)
        {
            string raisedLabel = bothProp.boolValue ? "On Event  (always)" : "On Pressed  ▶";
            EditorGUILayout.PropertyField(raisedProp, new GUIContent(raisedLabel));

            if (!bothProp.boolValue)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.PropertyField(releasedProp, new GUIContent("On Released  ◀"));
            }
        }
        else
        {
            EditorGUILayout.PropertyField(raisedProp, new GUIContent("On Raised  ▶"));
        }

        EditorGUILayout.Space(4);

        // ---- Play Mode status ----
        if (Application.isPlaying)
        {
            GUI.color = Color.cyan;
            EditorGUILayout.LabelField(
                $"✅ Subscribed to: {t.selectedEventTypeName}",
                EditorStyles.miniLabel);
            GUI.color = Color.white;
        }

        EditorGUILayout.Space(2);
        if (GUILayout.Button("↺  Refresh List"))
            RefreshCache(folder);

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Scans the source .cs file for "public bool pressed" — used in Edit Mode
    /// before Reflection's type cache is available.
    /// </summary>
    private static bool SourceHasPressedField(string folder, string className)
    {
        string fullPath = Path.Combine(Application.dataPath, folder);
        if (!Directory.Exists(fullPath)) return false;

        var classRegex  = new Regex($@"class\s+{Regex.Escape(className)}\b");
        var pressedRegex = new Regex(@"public\s+bool\s+pressed\b", RegexOptions.IgnoreCase);

        foreach (string file in Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(file);
            if (classRegex.IsMatch(content) && pressedRegex.IsMatch(content))
                return true;
        }
        return false;
    }

    private static void RefreshCache(string folder)
    {
        _cachedFolder = folder;
        _lastScanTime = EditorApplication.timeSinceStartup;

        string fullPath = Path.Combine(Application.dataPath, folder);
        if (!Directory.Exists(fullPath)) { _cachedNames = Array.Empty<string>(); return; }

        var regex = new Regex(@"public\s+class\s+(On\w+Event)\b", RegexOptions.Compiled);
        var names = new List<string>();

        foreach (string file in Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(file);
            foreach (Match m in regex.Matches(content))
                names.Add(m.Groups[1].Value);
        }

        _cachedNames = names.Distinct().OrderBy(n => n).ToArray();
    }
}


// ---- Invalidate if config asset is moved or deleted ----
public class EventBusListenerConfigWatcher : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Concat(deleted).Concat(moved).Any(p => p.Contains("EventBusListenerConfig")))
        {
            EventBusListenerConfig.Invalidate();
            EventBusListenerEditor.InvalidateCache();
        }
    }
}

#endif
