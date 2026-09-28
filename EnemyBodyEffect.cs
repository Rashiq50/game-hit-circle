using System.Numerics;
using Raylib_cs;

/// <summary>
/// Shades an enemy body drawn between Begin()/End(): the status looks (burning, frozen, shocked, poisoned, stunned), each
/// blended in by its own strength so several can stack, then the hit flash on top so a hit always reads. Alpha is kept
/// from the texel, so only the silhouette changes. Works for primitives too, since raylib draws shapes with a 1x1 white
/// texture and the colour in fragColor. One shader for everything because raylib has a single active shader and each
/// switch flushes the batch.
/// </summary>
static class EnemyBodyEffect
{
    // raylib's default vertex shader plus the world position: in 2D mode vertices arrive in world space, and primitives
    // sample a single white texel, so UVs are no use for patterns.
    const string VertexShader = """
        #version 330
        in vec3 vertexPosition;
        in vec2 vertexTexCoord;
        in vec4 vertexColor;
        uniform mat4 mvp;
        out vec2 fragTexCoord;
        out vec4 fragColor;
        out vec2 fragPos;

        void main()
        {
            fragTexCoord = vertexTexCoord;
            fragColor = vertexColor;
            fragPos = vertexPosition.xy;
            gl_Position = mvp * vec4(vertexPosition, 1.0);
        }
        """;

    const string FragmentShader = """
        #version 330
        in vec2 fragTexCoord;
        in vec4 fragColor;
        in vec2 fragPos;
        uniform sampler2D texture0;
        uniform vec4 colDiffuse;
        uniform vec2 center;
        uniform float radius;
        uniform float time;
        uniform float burn;   // status strengths, 0 = off, 1 = full
        uniform float freeze;
        uniform float shock;
        uniform float poison;
        uniform float stun;
        uniform float flash;  // 0 = untouched, 1 = solid flash colour
        uniform vec4 flashColor;
        out vec4 finalColor;

        float hash(vec2 p)
        {
            p = fract(p * vec2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return fract(p.x * p.y);
        }

        float noise(vec2 p)
        {
            vec2 i = floor(p);
            vec2 f = fract(p);
            vec2 u = f * f * (3.0 - 2.0 * f);
            return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
                       mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
        }

        float fbm(vec2 p) { return 0.65 * noise(p) + 0.35 * noise(p * 2.1 + 17.0); }

        float luma(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }

        void main()
        {
            vec4 texel = texture(texture0, fragTexCoord) * colDiffuse * fragColor;
            vec3 col = texel.rgb;
            vec2 local = (fragPos - center) / radius; // body space: roughly -1..1 across the body, +y is down

            if (poison > 0.0)
            {
                // Sickly green with dark purple blotches drifting slowly upward, pulsing like a heartbeat.
                float pulse = 0.8 + 0.2 * sin(time * 4.0);
                vec3 sick = luma(col) * vec3(0.55, 1.15, 0.35) + vec3(0.05, 0.14, 0.0);
                float blotch = smoothstep(0.55, 0.72, fbm(local * 2.5 + vec2(time * 0.15, time * 0.4)));
                sick = mix(sick, vec3(0.22, 0.07, 0.28), blotch * 0.65);
                col = mix(col, sick, poison * pulse);
            }

            if (freeze > 0.0)
            {
                // Remapped onto an ice-blue ramp with lifted shadows, plus sparse glints. Almost no motion: frozen solid.
                vec3 ice = mix(vec3(0.25, 0.45, 0.78), vec3(0.88, 0.97, 1.0), luma(col));
                vec2 grid = local * 5.0;
                float h = hash(floor(grid));
                float glint = step(0.86, h) * (0.5 + 0.5 * sin(time * 2.5 + h * 40.0));
                glint *= smoothstep(0.3, 0.0, length(fract(grid) - 0.5));
                col = mix(col, ice + glint * 0.8, freeze * 0.85);
            }

            if (burn > 0.0)
            {
                // Warmed toward orange, then flame noise scrolling upward, denser low on the body, with a fast flicker.
                float flicker = 0.85 + 0.15 * sin(time * 23.0) * sin(time * 7.3);
                col = mix(col, col * vec3(1.25, 0.7, 0.4), burn * 0.6);
                float n = fbm(local * vec2(3.0, 2.2) + vec2(0.0, time * 2.5));
                float low = smoothstep(-1.2, 1.0, local.y);
                float heat = smoothstep(0.35, 0.8, n * (0.6 + 0.6 * low));
                vec3 fire = mix(vec3(0.9, 0.15, 0.0), vec3(1.0, 0.85, 0.2), heat);
                col = mix(col, fire, heat * burn * flicker) + fire * heat * 0.25 * burn;
            }

            if (shock > 0.0)
            {
                // Strobing cyan wash with thin arcs that re-seed 20 times a second, so they jump like real sparks.
                float strobe = step(0.5, fract(time * 14.0));
                float seed = hash(vec2(floor(time * 20.0), 3.7)) * 50.0;
                float arc = noise(local * 4.0 + seed);
                float line = 1.0 - smoothstep(0.0, 0.045, abs(arc - 0.5));
                col = mix(col, vec3(0.6, 0.95, 1.0), shock * (0.25 + 0.35 * strobe));
                col = mix(col, vec3(0.92, 1.0, 1.0), line * shock * (0.6 + 0.4 * strobe));
            }

            if (stun > 0.0)
            {
                // Dazed: partly desaturated, a slow yellow pulse and a faint spiral turning around the body.
                col = mix(col, vec3(luma(col)), stun * 0.5);
                float pulse = 0.5 + 0.5 * sin(time * 6.0);
                float swirl = 0.5 + 0.5 * sin(atan(local.y, local.x) * 3.0 + length(local) * 6.0 - time * 5.0);
                col = mix(col, vec3(1.0, 0.9, 0.35), stun * (0.12 + 0.18 * pulse));
                col += vec3(1.0, 0.85, 0.2) * stun * smoothstep(0.7, 1.0, swirl) * 0.22;
            }

            col = mix(col, flashColor.rgb, flash);
            finalColor = vec4(clamp(col, 0.0, 1.0), texel.a);
        }
        """;

    // Uniform names in StatusEffect order.
    static readonly string[] StatusUniforms = ["burn", "freeze", "shock", "poison", "stun"];

    static Shader shader;
    static int centerLoc, radiusLoc, timeLoc, flashLoc, flashColorLoc;
    static readonly int[] statusLocs = new int[StatusEffects.Count];

    public static void Load()
    {
        shader = Raylib.LoadShaderFromMemory(VertexShader, FragmentShader);
        centerLoc = Raylib.GetShaderLocation(shader, "center");
        radiusLoc = Raylib.GetShaderLocation(shader, "radius");
        timeLoc = Raylib.GetShaderLocation(shader, "time");
        flashLoc = Raylib.GetShaderLocation(shader, "flash");
        flashColorLoc = Raylib.GetShaderLocation(shader, "flashColor");
        for (int i = 0; i < StatusEffects.Count; i++)
            statusLocs[i] = Raylib.GetShaderLocation(shader, StatusUniforms[i]);
    }

    public static void Unload() => Raylib.UnloadShader(shader);

    /// <summary>Uniforms are set before BeginShaderMode: setting them doesn't flush the batch, so they'd otherwise leak onto
    /// whatever was queued before.</summary>
    /// <param name="center">Body centre in world space; patterns are laid out around it.</param>
    /// <param name="radius">Rough body radius in world units, so patterns scale with the body.</param>
    /// <param name="time">Seconds on the enemy's own clock, which drives the animated looks.</param>
    /// <param name="statusLevels">Strength per status (0-1), indexed by <see cref="StatusEffect"/>.</param>
    public static void Begin(Vector2 center, float radius, float time, ReadOnlySpan<float> statusLevels, float flash, Color flashColor)
    {
        Raylib.SetShaderValue(shader, centerLoc, center, ShaderUniformDataType.Vec2);
        Raylib.SetShaderValue(shader, radiusLoc, radius, ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, timeLoc, time, ShaderUniformDataType.Float);
        for (int i = 0; i < StatusEffects.Count; i++)
            Raylib.SetShaderValue(shader, statusLocs[i], statusLevels[i], ShaderUniformDataType.Float);
        Raylib.SetShaderValue(shader, flashLoc, flash, ShaderUniformDataType.Float);
        var c = new Vector4(flashColor.R, flashColor.G, flashColor.B, flashColor.A) / 255f;
        Raylib.SetShaderValue(shader, flashColorLoc, c, ShaderUniformDataType.Vec4);
        Raylib.BeginShaderMode(shader);
    }

    public static void End() => Raylib.EndShaderMode();
}
