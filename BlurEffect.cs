using System.Numerics;
using Raylib_cs;

/// <summary>
/// Full-screen Gaussian blur. Wrap the drawing you want blurred in Begin()/End(): it goes to an offscreen texture,
/// which is then blurred (separable horizontal + vertical passes, repeated <see cref="Passes"/> times) and drawn to the screen.
/// </summary>
static class BlurEffect
{
    /// <summary>How many horizontal+vertical rounds to run; each round widens the blur.</summary>
    public const int Passes = 3;
    const float TapSpacing = 2f; // pixels between kernel taps; larger = wider (but grainier) blur per pass

    // 9-tap Gaussian, sampled along one axis; run twice (x then y) for a 2D blur at 2x the cost instead of 81 taps.
    const string FragmentShader = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        uniform vec2 direction; // (spacing / width, 0) for the horizontal pass, (0, spacing / height) for the vertical
        out vec4 finalColor;

        void main()
        {
            float weights[5] = float[](0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216);
            vec4 sum = texture(texture0, fragTexCoord) * weights[0];
            for (int i = 1; i < 5; i++)
            {
                sum += texture(texture0, fragTexCoord + direction * float(i)) * weights[i];
                sum += texture(texture0, fragTexCoord - direction * float(i)) * weights[i];
            }
            finalColor = sum * colDiffuse * fragColor;
        }
        """;

    static Shader shader;
    static int directionLoc;
    static RenderTexture2D scene, scratch; // ping-pong targets; recreated whenever the window size changes

    public static void Load()
    {
        shader = Raylib.LoadShaderFromMemory(null, FragmentShader);
        directionLoc = Raylib.GetShaderLocation(shader, "direction");
    }

    public static void Unload()
    {
        Raylib.UnloadShader(shader);
        if (scene.Id != 0) Raylib.UnloadRenderTexture(scene);
        if (scratch.Id != 0) Raylib.UnloadRenderTexture(scratch);
    }

    public static void Begin()
    {
        EnsureTargets();
        Raylib.BeginTextureMode(scene);
        Raylib.ClearBackground(Color.Black);
    }

    public static void End()
    {
        Raylib.EndTextureMode();

        var horizontal = new Vector2(TapSpacing / scene.Texture.Width, 0);
        var vertical = new Vector2(0, TapSpacing / scene.Texture.Height);
        for (int i = 0; i < Passes; i++)
        {
            BlurInto(scratch, scene, horizontal);
            BlurInto(scene, scratch, vertical);
        }
        Blit(scene.Texture);
    }

    static void BlurInto(RenderTexture2D target, RenderTexture2D source, Vector2 direction)
    {
        Raylib.BeginTextureMode(target);
        Raylib.BeginShaderMode(shader);
        Raylib.SetShaderValue(shader, directionLoc, direction, ShaderUniformDataType.Vec2);
        Blit(source.Texture);
        Raylib.EndShaderMode();
        Raylib.EndTextureMode();
    }

    /// <summary>Draws a render texture 1:1. Render textures are stored upside down, so the source rect flips them back.</summary>
    static void Blit(Texture2D texture)
    {
        var src = new Rectangle(0, 0, texture.Width, -texture.Height);
        Raylib.DrawTextureRec(texture, src, Vector2.Zero, Color.White);
    }

    static void EnsureTargets()
    {
        if (scene.Id != 0 && scene.Texture.Width == Screen.Width && scene.Texture.Height == Screen.Height) return;
        if (scene.Id != 0) Raylib.UnloadRenderTexture(scene);
        if (scratch.Id != 0) Raylib.UnloadRenderTexture(scratch);
        scene = Raylib.LoadRenderTexture(Screen.Width, Screen.Height);
        scratch = Raylib.LoadRenderTexture(Screen.Width, Screen.Height);
        // Bilinear so the taps between texels blend instead of snapping to the nearest pixel-art texel.
        Raylib.SetTextureFilter(scene.Texture, TextureFilter.Bilinear);
        Raylib.SetTextureFilter(scratch.Texture, TextureFilter.Bilinear);
    }
}
