using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>Primitive placeholder longboard: deck, grip, stripes, trucks, spinning oversized wheels.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        BoardController _board;
        Transform[] _wheels;
        float _wheelAngle;

        /// <summary>Parent for anything that stands on the deck; local (0, 0, 0) is deck-top centre.</summary>
        public Transform DeckTop { get; private set; }

        public static BoardView Create(BoardController board)
        {
            var view = board.gameObject.AddComponent<BoardView>();
            view.Build(board);
            return view;
        }

        void Build(BoardController board)
        {
            _board = board;
            BoardTuningData t = board.Tuning.data;
            float w = t.boardWidth, l = t.boardLength, r = t.wheelRadius, deckTop = t.deckHeight;
            const float deckThickness = 0.14f;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);

            // deck: middle slab + round nose and tail
            float deckY = deckTop - deckThickness * 0.5f;
            float slab = l - w;
            PrimitiveFactory.Visual(PrimitiveType.Cube, visual, new Vector3(0f, deckY, 0f), new Vector3(w, deckThickness, slab), MaterialLibrary.DeckWood, "Deck");
            PrimitiveFactory.Visual(PrimitiveType.Cylinder, visual, new Vector3(0f, deckY, slab * 0.5f), new Vector3(w, deckThickness * 0.5f, w), MaterialLibrary.DeckWood, "Nose");
            PrimitiveFactory.Visual(PrimitiveType.Cylinder, visual, new Vector3(0f, deckY, -slab * 0.5f), new Vector3(w, deckThickness * 0.5f, w), MaterialLibrary.DeckWood, "Tail");
            PrimitiveFactory.Visual(PrimitiveType.Cube, visual, new Vector3(0f, deckTop + 0.005f, 0f), new Vector3(w * 0.94f, 0.01f, slab), MaterialLibrary.DeckGrip, "Grip");
            for (int i = -1; i <= 1; i += 2)
                PrimitiveFactory.Visual(PrimitiveType.Cube, visual, new Vector3(0f, deckTop + 0.012f, i * slab * 0.22f), new Vector3(w * 0.94f, 0.01f, 0.35f), MaterialLibrary.DeckStripe, "Stripe");

            // trucks and wheels (front / rear axle at 35% of the length, like the ground probes)
            _wheels = new Transform[4];
            float axleZ = l * 0.35f;
            int k = 0;
            for (int zSign = -1; zSign <= 1; zSign += 2)
            {
                float z = zSign * axleZ;
                PrimitiveFactory.Visual(PrimitiveType.Cube, visual, new Vector3(0f, (r + deckTop - deckThickness) * 0.5f, z), new Vector3(0.35f, deckTop - deckThickness - r + 0.1f, 0.35f), MaterialLibrary.Metal, "TruckBase");
                PrimitiveFactory.Visual(PrimitiveType.Cube, visual, new Vector3(0f, r, z), new Vector3(w * 0.85f, 0.12f, 0.16f), MaterialLibrary.Metal, "Axle");
                for (int xSign = -1; xSign <= 1; xSign += 2)
                {
                    var hub = new GameObject("Wheel").transform;
                    hub.SetParent(visual, false);
                    hub.localPosition = new Vector3(xSign * (w * 0.5f - 0.05f), r, z);
                    GameObject tire = PrimitiveFactory.Visual(PrimitiveType.Cylinder, hub, Vector3.zero, new Vector3(r * 2f, 0.16f, r * 2f), MaterialLibrary.Wheel, "Tire");
                    tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    PrimitiveFactory.Visual(PrimitiveType.Cube, hub, new Vector3(xSign * 0.17f, 0f, 0f), new Vector3(0.02f, r * 1.2f, 0.12f), MaterialLibrary.LineWhite, "Spoke");
                    _wheels[k++] = hub;
                }
            }

            DeckTop = new GameObject("DeckTop").transform;
            DeckTop.SetParent(transform, false);
            DeckTop.localPosition = new Vector3(0f, deckTop + 0.01f, 0f);
        }

        void Update()
        {
            if (_board == null || _board.Simulation == null) return;
            BoardState s = _board.State;
            float r = Mathf.Max(_board.Tuning.data.wheelRadius, 0.05f);
            if (s.Grounded || s.Crashed) _wheelAngle += s.Speed / r * Mathf.Rad2Deg * Time.deltaTime;
            _wheelAngle %= 360f;
            for (int i = 0; i < _wheels.Length; i++) _wheels[i].localRotation = Quaternion.Euler(_wheelAngle, 0f, 0f);
        }
    }
}
