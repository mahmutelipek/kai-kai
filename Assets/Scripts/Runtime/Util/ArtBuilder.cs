using System.Collections.Generic;
using Game.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game
{
    /// <summary>
    /// Instantiates an ArtModel as a GameObject hierarchy: one child per group (named like the group, placed at
    /// its pivot, parented like the model says) and one renderer per group with the shared palette materials.
    /// Views animate the group transforms (arms, torso, wheels, "Scaled").
    /// </summary>
    public static class ArtBuilder
    {
        public const string RootName = "Body";

        /// <summary>Builds <paramref name="model"/> under <paramref name="parent"/>; returns the transform of every group by name ("" = root parts).</summary>
        public static Dictionary<string, Transform> Build(ArtModel model, Transform parent, bool castShadows = true)
        {
            var groups = new Dictionary<string, Transform>();
            if (HasParts(model, "")) groups[""] = MakeNode(RootName, parent, Vector3.zero, model, "", castShadows);
            else groups[""] = parent;
            foreach (ArtGroup g in model.Groups) Ensure(model, g.Name, parent, groups, castShadows);
            return groups;
        }

        static Transform Ensure(ArtModel model, string name, Transform root, Dictionary<string, Transform> groups, bool castShadows)
        {
            if (groups.TryGetValue(name, out Transform t) && (name.Length > 0 || t != null)) return t;
            ArtGroup g = default;
            foreach (ArtGroup candidate in model.Groups) if (candidate.Name == name) { g = candidate; break; }
            Transform parent = string.IsNullOrEmpty(g.Parent) ? root : Ensure(model, g.Parent, root, groups, castShadows);
            t = MakeNode(name, parent, new Vector3(g.Pivot.X, g.Pivot.Y, g.Pivot.Z), model, name, castShadows);
            groups[name] = t;
            return t;
        }

        static Transform MakeNode(string name, Transform parent, Vector3 localPos, ArtModel model, string group, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            if (HasParts(model, group))
            {
                go.AddComponent<MeshFilter>().sharedMesh = ArtMeshes.GroupMesh(model, group);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = ArtMeshes.Materials;
                r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            return go.transform;
        }

        static bool HasParts(ArtModel model, string group)
        {
            foreach (ArtPart p in model.Parts) if (p.Group == group) return true;
            return false;
        }

        /// <summary>A static mesh object (chunk, backdrop) with the shared palette materials.</summary>
        public static MeshRenderer MeshObject(string name, Transform parent, Mesh mesh, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = ArtMeshes.Materials;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return r;
        }
    }
}
