namespace Game.Simulation
{
    public enum SurfaceKind
    {
        Road = 0,
        Offroad = 1,
    }

    public struct GroundSample
    {
        public bool Found;
        public float Height;
        public SurfaceKind Surface;

        public static GroundSample At(float height, SurfaceKind surface = SurfaceKind.Road) =>
            new GroundSample { Found = true, Height = height, Surface = surface };

        public static readonly GroundSample Missing = default;
    }

    /// <summary>
    /// Supplies ground height under a world XZ point. Unity implements it with raycasts,
    /// headless tests with analytic planes. <paramref name="searchFromHeight"/> is the height the
    /// query starts from (samples above it, e.g. a tunnel roof, must be ignored).
    /// </summary>
    public interface IGroundProvider
    {
        GroundSample Sample(float x, float z, float searchFromHeight);
    }

    /// <summary>Infinite sloped plane: height = -grade * z. Used by headless tests.</summary>
    public sealed class SlopedPlaneGround : IGroundProvider
    {
        public float Grade;
        public SurfaceKind Surface;

        public SlopedPlaneGround(float grade = 0.05f, SurfaceKind surface = SurfaceKind.Road)
        {
            Grade = grade;
            Surface = surface;
        }

        public GroundSample Sample(float x, float z, float searchFromHeight) => GroundSample.At(-Grade * z, Surface);
    }
}
