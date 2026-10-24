namespace JaxClient_v3.BodyRendering;

/// <summary>
/// Cube model.
/// </summary>
public static class CubeModel
{
    public const float Value = 0.5f;

    public static readonly float[] Vertices =
    [
        0.0f,  0.0f, -1.0f,
        0.0f,  0.0f, -1.0f,
        0.0f,  0.0f, -1.0f,
        0.0f,  0.0f, -1.0f,

        0.0f,  0.0f,  1.0f,
        0.0f,  0.0f,  1.0f,
        0.0f,  0.0f,  1.0f,
        0.0f,  0.0f,  1.0f,

        -1.0f,  0.0f,  0.0f,
        -1.0f,  0.0f,  0.0f,
        -1.0f,  0.0f,  0.0f,
        -1.0f,  0.0f,  0.0f,

        1.0f,  0.0f,  0.0f,
        1.0f,  0.0f,  0.0f,
        1.0f,  0.0f,  0.0f,
        1.0f,  0.0f,  0.0f,

        0.0f,  1.0f,  0.0f,
        0.0f,  1.0f,  0.0f,
        0.0f,  1.0f,  0.0f,
        0.0f,  1.0f,  0.0f,

        0.0f, -1.0f,  0.0f,
        0.0f, -1.0f,  0.0f,
        0.0f, -1.0f,  0.0f,
        0.0f, -1.0f,  0.0f,
    ];

    private static readonly float[] _cube =
    [
        Value, Value, -Value, /* Back face. */
        Value, -Value, -Value,
        -Value, -Value, -Value,
        -Value, Value, -Value,
        -Value, Value, Value, /* Front face. */
        -Value, -Value, Value,
        Value, -Value, Value,
        Value, Value, Value,
        -Value, Value, -Value, /* Left face. */
        -Value, -Value, -Value,
        -Value, -Value, Value,
        -Value, Value, Value,
        Value, Value, Value, /* Right face. */
        Value, -Value, Value,
        Value, -Value, -Value,
        Value, Value, -Value,
        -Value, Value, -Value, /* Top face. */
        -Value, Value, Value,
        Value, Value, Value,
        Value, Value, -Value,
        Value, -Value, -Value, /* Bottom face. */
        Value, -Value, Value,
        -Value, -Value, Value,
        -Value, -Value, -Value,
    ];

    private static readonly ushort[] _cubeIndicies =
    [
       0, 1, 2, 0, 2, 3,
       4, 5, 6, 4, 6, 7,
       8, 9, 10, 8, 10, 11,
       12, 13, 14, 12, 14, 15,
       16, 17, 18, 16, 18, 19,
       20, 21, 22, 20, 22, 23
    ];

    /// <summary>
    /// Gets the XYZ coordinates of a cube.
    /// </summary>
    /// <param name="multiplyX"></param>
    /// <param name="multiplyY"></param>
    /// <param name="multiplyZ"></param>
    /// <param name="addX"></param>
    /// <param name="addY"></param>
    /// <param name="addZ"></param>
    /// <param name="enlarge"></param>
    /// <returns></returns>
    public static float[] GetSquare(float multiplyX = 1.0f, float multiplyY = 1.0f, float multiplyZ = 1.0f,
        float addX = 0.0f, float addY = 0.0f, float addZ = 0.0f, float enlarge = 1.0f)
    {
        var temp = new float[_cube.Length];
        for (int a = 0; a < temp.Length; a++)
        {
            temp[a] = _cube[a] * enlarge;
            if (a % 3 == 0)
            {
                temp[a] = temp[a] * multiplyX + addX;
            }
            else if (a % 3 == 1)
            {
                temp[a] = temp[a] * multiplyY + addY;
            }
            else
            {
                temp[a] = temp[a] * multiplyZ + addZ;
            }
        }

        return temp;
    }

    /// <summary>
    /// Gets the standard cube vertex indices.
    /// </summary>
    /// <param name="offset"></param>
    /// <returns></returns>
    public static ushort[] GetSquareIndicies(int offset = 0)
    {
        var temp = new ushort[_cubeIndicies.Length];
        for (int a = 0; a < temp.Length; a++)
        {
            temp[a] = (ushort)(_cubeIndicies[a] + offset);
        }

        return temp;
    }
}
