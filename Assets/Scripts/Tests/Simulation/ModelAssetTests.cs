using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Game.Art;
using NUnit.Framework;

namespace Game.Tests
{
    /// <summary>
    /// The Blender-made models (Assets/Resources/Models, Tools/Blender/build_models.py) drop into the procedural
    /// rig: same groups and pivots, feet on the ground, sensible size, valid triangles, inside the triangle budget.
    /// </summary>
    public class ModelAssetTests
    {
        static string ModelsDir()
        {
            string dir = TestContext.CurrentContext.TestDirectory;
            for (int i = 0; i < 8 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            {
                string p = Path.Combine(dir, "Assets", "Resources", "Models");
                if (Directory.Exists(p)) return p;
            }
            return null;
        }

        static ArtModel Load(string name)
        {
            string dir = ModelsDir();
            string f = dir == null ? null : Path.Combine(dir, name + ".bytes");
            if (f == null || !File.Exists(f)) Assert.Ignore(name + ".bytes not exported yet");
            return ArtAsset.Read(File.ReadAllBytes(f));
        }

        static (Vector3 min, Vector3 max, int tris) Bake(ArtModel m)
        {
            var set = new MeshSet();
            set.Add(m, Vector3.Zero, Quaternion.Identity);
            Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            int tris = 0;
            foreach (MeshData d in set.ByColor.Values)
            {
                foreach (Vector3 v in d.Vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
                tris += d.Triangles.Count / 3;
                for (int i = 0; i < d.Triangles.Count; i++) Assert.That(d.Triangles[i], Is.InRange(0, d.VertexCount - 1));
            }
            return (min, max, tris);
        }

        [Test]
        public void Rider_MatchesTheProceduralRig_StandsOnTheGround_InBudget()
        {
            ArtModel rider = Load("Rider0");
            ArtLibrary.ModelSource = null; ArtLibrary.ClearCache();
            ArtModel procedural = ArtLibrary.Character(0);
            foreach (ArtGroup g in procedural.Groups)
            {
                ArtGroup a = rider.Groups.Find(x => x.Name == g.Name);
                Assert.AreEqual(g.Name, a.Name, "group " + g.Name);
                Assert.AreEqual(g.Parent, a.Parent, "parent of " + g.Name);
                Assert.That(Vector3.Distance(g.Pivot, a.Pivot), Is.LessThan(1e-3f), "pivot of " + g.Name);
            }
            (Vector3 min, Vector3 max, int tris) = Bake(rider);
            (Vector3 pmin, Vector3 pmax, int ptris) = Bake(procedural);
            TestContext.WriteLine($"Rider0: {rider.Parts.Count} parts, {tris} tris, bounds {min} .. {max}; procedural {ptris} tris, {pmin} .. {pmax}");
            Assert.That(min.Y, Is.InRange(-0.01f, 0.02f), "feet on the deck");
            Assert.That(max.Y, Is.InRange(1.6f, 1.95f), "about as tall as the procedural rider");
            Assert.That(max.X - min.X, Is.LessThan(1.0f));
            Assert.That(tris, Is.LessThan(20000), "triangle budget per rider");
            foreach (ArtPart p in rider.Parts) Assert.IsNotNull(rider.Groups.Find(g => g.Name == p.Group).Name, p.Name + " sits in a known group");
        }

        [Test]
        public void Board_HasFourWheelHubs_FitsTheTunedDeck()
        {
            ArtModel board = Load("Board");
            for (int k = 0; k < 4; k++) Assert.IsTrue(board.Groups.Exists(g => g.Name == "Wheel" + k), "Wheel" + k);
            (Vector3 min, Vector3 max, int tris) = Bake(board);
            TestContext.WriteLine($"Board: {board.Parts.Count} parts, {tris} tris, bounds {min} .. {max}");
            Assert.That(max.X - min.X, Is.InRange(ArtLibrary.BoardAssetWidth, ArtLibrary.BoardAssetWidth + 0.4f), "deck plus wheels sticking out a little");
            Assert.That(max.Z - min.Z, Is.InRange(ArtLibrary.BoardAssetLength - 0.1f, ArtLibrary.BoardAssetLength + 0.4f));
            Assert.That(min.Y, Is.InRange(-0.02f, 0.02f), "wheels touch the ground");
            Assert.That(max.Y, Is.InRange(ArtLibrary.BoardAssetDeckTop, ArtLibrary.BoardAssetDeckTop + 0.05f), "deck top where riders stand");
            Assert.That(tris, Is.LessThan(20000));

            // the library stretches it to another tuned size, wheels follow the deck edge
            byte[] bytes = File.ReadAllBytes(Path.Combine(ModelsDir(), "Board.bytes"));
            ArtLibrary.ModelSource = n => n == "Board" ? bytes : null;
            ArtLibrary.ClearCache();
            try
            {
                ArtModel wide = ArtLibrary.Board(3.0f, 7.5f, 0.85f, 0.35f);
                CollectionAssert.Contains(ArtLibrary.LoadedAssets, "Board");
                (Vector3 wmin, Vector3 wmax, _) = Bake(wide);
                Assert.That(wmax.X - wmin.X, Is.InRange(3.0f, 3.4f));
                Assert.That(wmax.Z - wmin.Z, Is.InRange(7.4f, 7.95f));
            }
            finally { ArtLibrary.ModelSource = null; ArtLibrary.ClearCache(); }
        }

        [Test]
        public void BrokenOrMissingFiles_FallBackToProcedural()
        {
            ArtLibrary.ModelSource = n => n == "Rider1" ? new byte[] { 1, 2, 3 } : null;
            ArtLibrary.ClearCache();
            try
            {
                ArtModel r1 = ArtLibrary.Character(1), r2 = ArtLibrary.Character(2);
                Assert.That(r1.Parts.Count, Is.GreaterThan(10));
                Assert.That(r2.Parts.Count, Is.GreaterThan(10));
                Assert.IsEmpty(ArtLibrary.LoadedAssets);
                Assert.Throws<InvalidDataException>(() => ArtAsset.Read(new byte[] { (byte)'X', 0, 0, 0, 0 }));
            }
            finally { ArtLibrary.ModelSource = null; ArtLibrary.ClearCache(); }
        }
    }
}
