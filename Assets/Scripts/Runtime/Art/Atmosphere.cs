using System.Numerics;

namespace Game.Art
{
    /// <summary>
    /// Look of the sunny coastal afternoon, shared by the Unity scene setup and the headless preview.
    /// Directions are relative to the backdrop frame, which slowly follows the travel direction (+Z = downhill).
    /// </summary>
    public static class Atmosphere
    {
        public static readonly ArtColor SkyTop = ArtColor.Hex(0x3F92E6);
        public static readonly ArtColor SkyHorizon = ArtColor.Hex(0xCBE9FF);
        public static readonly ArtColor FogColor = ArtColor.Hex(0xC6E2F7);
        public static readonly ArtColor AmbientSky = ArtColor.Hex(0xCFE3FF);
        public static readonly ArtColor AmbientEquator = ArtColor.Hex(0xF2E8D5);
        public static readonly ArtColor AmbientGround = ArtColor.Hex(0x93A176);
        public static readonly ArtColor SunColor = ArtColor.Hex(0xFFF0D9);

        public const float SunIntensity = 1.35f;
        public const float FogStart = 180f;
        public const float FogEnd = 1400f;

        /// <summary>Direction the sunlight travels: from high behind-left of the chase camera, so riders' backs are lit.</summary>
        public static Vector3 SunForward => Vector3.Normalize(new Vector3(0.45f, -0.78f, 0.45f));
    }
}

namespace Game.Art
{
    /// <summary>Chase camera framing shared by CameraController and the headless preview.</summary>
    public static class CameraRigDefaults
    {
        // M4: a low 3/4 view from behind-right like the reference image (was 7 m behind, 3.5 m up, centred)
        public const float Distance = 4.7f;
        public const float ExtraDistanceAtSpeed = 1.2f;
        public const float Height = 2.6f;
        public const float LookAhead = 10f;
        public const float LookHeight = 1.4f;
        /// <summary>Camera sits this far to the right of the board; it also slides toward the outside of turns.</summary>
        public const float LateralOffset = 1.8f;
        public const float FovMin = 70f;
        public const float FovMax = 86f;
    }
}
