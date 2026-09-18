using Raylib_cs;

/// <summary>
/// Desaturates whatever is drawn between Begin()/End(). Used to wash the background out while the player
/// picks an ultimate target, so the (still coloured) enemies stand out against it.
/// </summary>
static class GrayscaleEffect
{
    // Luma weights (Rec. 601) so the result reads as brightness rather than a flat average.
    const string FragmentShader = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        out vec4 finalColor;

        void main()
        {
            vec4 texel = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
            float gray = dot(texel.rgb, vec3(0.299, 0.587, 0.114));
            finalColor = vec4(vec3(gray), texel.a);
        }
        """;

    static Shader shader;

    public static void Load() => shader = Raylib.LoadShaderFromMemory(null, FragmentShader);
    public static void Unload() => Raylib.UnloadShader(shader);

    public static void Begin() => Raylib.BeginShaderMode(shader);
    public static void End() => Raylib.EndShaderMode();
}
