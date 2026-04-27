using UnityEngine;

public abstract class InputEventBase
{
    public bool pressed;
}

public enum LookInputSource
{
    Mouse,
    Gamepad
}

// ============================================================================
// INPUT EVENTS
// ============================================================================

public class OnMoveInputEvent : InputEventBase
{
    public Vector2 Direction;
}

public class OnLookInputEvent : InputEventBase
{
    public Vector2 Delta;
    public LookInputSource Source;
}

public class OnActionInputEvent : InputEventBase
{
}

public class OnCrouchInputEvent : InputEventBase
{
}

public class OnJumpInputEvent : InputEventBase
{
}

public class OnShootInputEvent : InputEventBase
{
}

public class OnAimInputEvent : InputEventBase
{
}

public class OnOpenInventoryEvent : InputEventBase
{
}

// ============================================================================
// INVENTORY INPUT EVENTS
// ============================================================================

public class OnReloadKeyEvent : InputEventBase
{
}

public class OnRotateKeyEvent : InputEventBase
{
}

public class OnLeftClickEvent : InputEventBase
{
}

public class OnRightClickEvent : InputEventBase
{
}

public class OnPointerPositionEvent : InputEventBase
{
    public Vector2 Position; // en píxeles, coordenadas de pantalla
}