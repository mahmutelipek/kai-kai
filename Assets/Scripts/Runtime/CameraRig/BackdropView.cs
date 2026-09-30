using Game.Art;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// The far scenery (bay, bridge, skyline, hills, clouds): one mesh built once from Game.Art.Backdrop that
    /// follows the board and turns slowly with the travel direction, so the bay always opens up ahead-right.
    /// The sun turns with it, keeping the riders lit from behind-left. Purely visual.
    /// </summary>
    public sealed class BackdropView : MonoBehaviour
    {
        const float YawTimeConstant = 12f;

        BoardController _board;
        Light _sun;
        float _yaw;
        bool _snapped;

        public static BackdropView Create(Transform parent, BoardController board, Light sun)
        {
            var go = new GameObject("Backdrop");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BackdropView>();
            view._board = board;
            view._sun = sun;
            var mesh = new Mesh { name = "Backdrop" };
            ArtMeshes.Upload(Backdrop.Build(), mesh);
            mesh.UploadMeshData(true);
            MeshRenderer r = ArtBuilder.MeshObject("Geometry", go.transform, mesh, castShadows: false);
            r.receiveShadows = false;
            return view;
        }

        void LateUpdate()
        {
            if (_board == null || _board.Simulation == null) return;
            float travel = _board.State.TravelYaw * Mathf.Rad2Deg;
            if (!_snapped) { _yaw = travel; _snapped = true; }
            _yaw = Mathf.LerpAngle(_yaw, travel, 1f - Mathf.Exp(-Time.deltaTime / YawTimeConstant));
            Vector3 p = _board.transform.position;
            transform.SetPositionAndRotation(p, Quaternion.Euler(0f, _yaw, 0f));
            if (_sun != null)
            {
                System.Numerics.Vector3 f = Atmosphere.SunForward;
                _sun.transform.rotation = Quaternion.Euler(0f, _yaw, 0f) * Quaternion.LookRotation(new Vector3(f.X, f.Y, f.Z));
            }
        }

        /// <summary>Re-centres immediately (after a restart).</summary>
        public void Snap() => _snapped = false;
    }
}
