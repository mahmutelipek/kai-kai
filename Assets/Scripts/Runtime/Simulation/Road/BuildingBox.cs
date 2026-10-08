namespace Game.Simulation
{
    /// <summary>A house or apartment beside the road: local along range, side (-1 left / +1 right) and the offsets of its front / back face outside the road edge.</summary>
    public struct BuildingBox
    {
        public float AlongMin, AlongMax;
        public int Side;
        public float Near, Far;
    }

    /// <summary>A solid pole or trunk beside the road (street light, sign, palm, tree): local along, signed lateral offset from the centre line, radius.</summary>
    public struct PostCircle
    {
        public float Along, Lateral, Radius;
    }
}
