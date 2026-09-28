using System.Numerics;
using Raylib_cs;

/// <summary>
/// Pushes whatever is drawn between Begin()/End() toward a flat colour while keeping its alpha, so the silhouette flashes.
/// A tint can't do this: raylib multiplies by it, so white is the brightest it gets. Works for primitives too, since
/// raylib draws shapes with a 1x1 white texture and the colour in fragColor.
/// </summary>
static class HitFlashEffect
{
    const string FragmentShader = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        uniform float amount; // 0 = untouched, 1 = solid flash colour
        uniform vec4 flashColor;
        out vec4 finalColor;

        void main()
        {
            vec4 texel = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
            finalColor = vec4(mix(texel.rgb, flashColor.rgb, amount), texel.a);
        }
        """;

    static Shader shader;
    static int amountLoc, colorLoc;

    public static void Load()
    {
        shader = Raylib.LoadShaderFromMemory(null, FragmentShader);
        amountLoc = Raylib.GetShaderLocation(shader, "amount");
        colorLoc = Raylib.GetShaderLocation(shader, "flashColor");
    }

    public static void Unload() => Raylib.UnloadShader(shader);

    /// <summary>Uniforms are set before BeginShaderMode: setting them doesn't flush the batch, so they'd otherwise leak onto
    /// whatever was queued before.</summary>
    public static void Begin(float amount, Color color)
    {
        Raylib.SetShaderValue(shader, amountLoc, amount, ShaderUniformDataType.Float);
        var c = new Vector4(color.R, color.G, color.B, color.A) / 255f;
        Raylib.SetShaderValue(shader, colorLoc, c, ShaderUniformDataType.Vec4);
        Raylib.BeginShaderMode(shader);
    }

    public static void End() => Raylib.EndShaderMode();
}
