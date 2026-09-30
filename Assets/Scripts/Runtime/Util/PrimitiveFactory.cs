using UnityEngine;

namespace Game
{
    /// <summary>Unity primitives for debug markers and simple effects (the game art lives in Game.Art, see ArtBuilder).</summary>
    public static class PrimitiveFactory
    {
        /// <summary>Creates a primitive child without a collider (visual only).</summary>
        public static GameObject Visual(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Color color, string name = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name ?? type.ToString();
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = MaterialLibrary.Get(color);
            return go;
        }
    }
}
