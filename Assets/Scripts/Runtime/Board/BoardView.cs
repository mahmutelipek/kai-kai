using Game.Art;
using Game.Simulation;
using UnityEngine;

namespace Game
{
    /// <summary>The giant longboard: stylised model, spinning oversized wheels, landing squash-and-stretch.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        BoardController _board;
        Transform[] _wheels;
        Transform _visual;
        float _wheelAngle;
        float _squash, _squashVelocity;

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

            var visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            _visual = visual;
            board.Landed += speed => _squashVelocity -= Mathf.Clamp(speed * 0.12f, 0f, 1.6f);

            // M4: stylised longboard from Game.Art (deck, grip, red bands, trucks, groups "Wheel0".."Wheel3")
            var parts = ArtBuilder.Build(ArtLibrary.Board(w, l, deckTop, r), visual);
            _wheels = new Transform[4];
            for (int k = 0; k < 4; k++) _wheels[k] = parts["Wheel" + k];

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

            // landing squash-and-stretch spring (visual only)
            float dt = Time.deltaTime;
            _squashVelocity += (-160f * _squash - 12f * _squashVelocity) * dt;
            _squash = Mathf.Clamp(_squash + _squashVelocity * dt, -0.3f, 0.3f);
            _visual.localScale = new Vector3(1f - _squash * 0.4f, 1f + _squash, 1f - _squash * 0.2f);
        }
    }
}
