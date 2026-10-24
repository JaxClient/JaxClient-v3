namespace JaxClient_v3.Rendering;

/// <summary>
/// Skin type.
/// </summary>
public enum SkinType
{
    /// <summary>
    /// Legacy 1.7 skin.
    /// </summary>
    Old,
    /// <summary>
    /// Standard 1.8 skin.
    /// </summary>
    New,
    /// <summary>
    /// Slim 1.8 skin.
    /// </summary>
    NewSlim,
    /// <summary>
    /// Unknown skin type.
    /// </summary>
    Unkonw
}

/// <summary>
/// Mouse button.
/// </summary>
public enum KeyType
{
    /// <summary>
    /// No button pressed.
    /// </summary>
    None,
    /// <summary>
    /// Left button.
    /// </summary>
    Left,
    /// <summary>
    /// Right button.
    /// </summary>
    Right
}

/// <summary>
/// Error type.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Unknown skin type.
    /// </summary>
    UnknowSkinType,
    /// <summary>
    /// Skin not found.
    /// </summary>
    SkinNotFound
}

/// <summary>
/// Current state.
/// </summary>
public enum StateType
{
    SkinReload
}

/// <summary>
/// Model component.
/// </summary>
public enum ModelPartType
{
    Head, Body, LeftArm, RightArm, LeftLeg, RightLeg, Cape,
    Proj, View, Model
}

/// <summary>
/// Render mode.
/// </summary>
public enum SkinRenderType
{
    Normal, MSAA, FXAA
}