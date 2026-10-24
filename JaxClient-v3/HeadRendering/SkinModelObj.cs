namespace JaxClient_v3.Rendering;
/// <summary>
/// Cube model data.
/// </summary>
public record CubeModelItemObj
{
    public float[] Model;
    public ushort[] Point;
}

/// <summary>
/// Steve model data.
/// </summary>
public record SteveModelObj
{
    public CubeModelItemObj Head;
    public CubeModelItemObj Body;
    public CubeModelItemObj LeftArm;
    public CubeModelItemObj RightArm;
    public CubeModelItemObj LeftLeg;
    public CubeModelItemObj RightLeg;
    public CubeModelItemObj Cape;
}

/// <summary>
/// Model texture data.
/// </summary>
public record SteveTextureObj
{
    public float[] Head;
    public float[] Body;
    public float[] LeftArm;
    public float[] RightArm;
    public float[] LeftLeg;
    public float[] RightLeg;
    public float[] Cape;
}