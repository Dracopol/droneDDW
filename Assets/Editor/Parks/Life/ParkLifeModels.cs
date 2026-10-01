using System.Linq;
using DroneDDW.Parks.Editor.Building;
using DroneDDW.Parks.Life;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DroneDDW.Parks.Editor.Life
{
	/// <summary>
	/// The park's animals as low-poly, flat-coloured models in the kit's style, built from code: boxes, tapered bodies,
	/// beaks and two-sided wing plates, every face on a palette swatch (no textures). Each is one skinned mesh with wing,
	/// head and tail bones where it has them, saved as a mesh asset and a prefab with a <see cref="ParkCreature"/>.
	/// Rebuilt on every park build; existing assets are updated in place, so references to them hold.
	/// </summary>
	internal static class ParkLifeModels
	{
		#region Entities

		/// <summary>A bird's colours by palette swatch.</summary>
		private readonly struct Plumage
		{
			public Plumage(string back, string belly, string breast, string head, string face, string beak, string wing,
				string tail)
			{
				Back = back;
				Belly = belly;
				Breast = breast;
				Head = head;
				Face = face;
				Beak = beak;
				Wing = wing;
				Tail = tail;
			}

			public string Back { get; }
			public string Belly { get; }
			public string Breast { get; }
			public string Head { get; }
			public string Face { get; }
			public string Beak { get; }
			public string Wing { get; }
			public string Tail { get; }
		}

		#endregion

		#region Fields

		internal const string ModelsFolder = ParkAssetUtility.ModelsFolder + "/Life";
		internal const string PrefabsFolder = ParkAssetUtility.PrefabsFolder + "/Life";

		private const string PaletteName = "Park_Palette.png";
		private const string PropsMaterialName = "Park_Mat_Props.mat";
		private const string Eye = "10141a";
		private const string Leg = "6a4a2e";
		private const int Root = 0;
		private const int WingLeft = 1;
		private const int WingRight = 2;
		private const int Head = 3;
		private const int Tail = 4;
		private const float BoundsMargin = 1.6f;

		private static readonly string[] BoneNames = { string.Empty, "Wing L", "Wing R", "Head", "Tail" };

		// Back, belly, breast, head, face, beak, wing, tail
		private static readonly Plumage Robin =
			new Plumage("7a5c3e", "f4f1ea", "ee9a3a", "7a5c3e", "ee9a3a", "3b3631", "6b4a33", "6b4a33");
		private static readonly Plumage Blackbird =
			new Plumage("22303b", "22303b", "22303b", "22303b", "22303b", "f2c230", "22303b", "22303b");
		private static readonly Plumage BlueTit =
			new Plumage("6c8f55", "edd244", "edd244", "5a8bd8", "f5f3ee", "3b3631", "5a8bd8", "5a8bd8");
		private static readonly Plumage Sparrow =
			new Plumage("9c6d45", "9c9c94", "9c9c94", "7d7d76", "9c9c94", "3b3631", "7a5c3e", "7a5c3e");
		private static readonly Plumage Pigeon =
			new Plumage("9c9c94", "aeb3b8", "8e949a", "8e949a", "8e949a", "5f666e", "8e949a", "5f666e");
		private static readonly Plumage Crow =
			new Plumage("22303b", "22303b", "22303b", "22303b", "22303b", "3c4147", "22303b", "22303b");
		private static readonly Plumage Woodpecker =
			new Plumage("22303b", "f4f1ea", "f4f1ea", "d9463f", "f4f1ea", "3c4147", "22303b", "22303b");

		#endregion

		#region Internal

		internal static ParkCreaturePrefabs Create()
		{
			ParkPalette palette = ParkPalette.Load($"{ParkAssetUtility.TexturesFolder}/{PaletteName}");
			string materialPath = $"{ParkAssetUtility.MaterialsFolder}/{PropsMaterialName}";
			Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
			ParkAssetUtility.EnsureFolder(ModelsFolder);
			ParkAssetUtility.EnsureFolder(PrefabsFolder);
			ParkCreaturePrefabs prefabs = new ParkCreaturePrefabs
			{
				Songbirds = new[]
				{
					Songbird(palette, material, "Park_Life_Robin", 1f, Robin),
					Songbird(palette, material, "Park_Life_Blackbird", 1.15f, Blackbird),
					Songbird(palette, material, "Park_Life_BlueTit", 0.85f, BlueTit),
					Songbird(palette, material, "Park_Life_Sparrow", 0.95f, Sparrow),
				},
				Pigeon = Songbird(palette, material, "Park_Life_Pigeon", 2.2f, Pigeon),
				Crow = Songbird(palette, material, "Park_Life_Crow", 2.5f, Crow),
				Woodpecker = Songbird(palette, material, "Park_Life_Woodpecker", 1.3f, Woodpecker),
				Buzzard = Buzzard(palette, material),
				Ducks = new[]
				{
					Duck(palette, material, "Park_Life_DuckDrake", true),
					Duck(palette, material, "Park_Life_DuckHen", false),
				},
				Frog = Frog(palette, material),
				Fish = Fish(palette, material),
				Bee = Bee(palette, material),
				Butterflies = new[]
				{
					Butterfly(palette, material, "Park_Life_ButterflyOrange", "ee9a3a", "22303b"),
					Butterfly(palette, material, "Park_Life_ButterflyWhite", "f3f5f7", "9c9c94"),
					Butterfly(palette, material, "Park_Life_ButterflyBlue", "79d6f2", "3e73bf"),
				},
				Dragonfly = Dragonfly(palette, material),
			};
			AssetDatabase.SaveAssets();
			return prefabs;
		}

		#endregion

		#region Private

		/// <summary>A perching bird, standing at the origin, about 15 cm long at scale 1.</summary>
		private static ParkCreature Songbird(ParkPalette palette, Material material, string name, float scale,
			Plumage colors)
		{
			ParkMeshBuilder mesh = new ParkMeshBuilder(palette);
			Vector3 S(float x, float y, float z) => new Vector3(x, y, z) * scale;
			Vector3 wingLeft = S(-0.028f, 0.06f, 0.012f), wingRight = S(0.028f, 0.062f, 0.012f);

			mesh.Bone = Root;
			mesh.Frustum(S(0f, 0.047f, -0.04f), new Vector2(0.042f, 0.04f) * scale, S(0f, 0.045f, 0.035f),
				new Vector2(0.06f, 0.056f) * scale, colors.Back, colors.Back, colors.Belly, colors.Breast, colors.Back);
			mesh.Box(S(-0.012f, 0.012f, 0.008f), S(0.006f, 0.024f, 0.006f), Leg);
			mesh.Box(S(0.012f, 0.012f, 0.008f), S(0.006f, 0.024f, 0.006f), Leg);

			mesh.Bone = Head;
			mesh.Box(S(0f, 0.08f, 0.05f), S(0.044f, 0.042f, 0.044f), colors.Head, colors.Head, colors.Face, colors.Face,
				colors.Head);
			mesh.Pyramid(S(0f, 0.076f, 0.072f), new Vector2(0.016f, 0.012f) * scale, S(0f, 0.074f, 0.094f), colors.Beak);
			mesh.Box(S(-0.0225f, 0.086f, 0.058f), S(0.007f, 0.007f, 0.007f), Eye);
			mesh.Box(S(0.0225f, 0.086f, 0.058f), S(0.007f, 0.007f, 0.007f), Eye);

			mesh.Bone = Tail;
			mesh.Panel(colors.Tail, colors.Tail, Vector3.up, S(-0.016f, 0.052f, -0.038f), S(0.016f, 0.052f, -0.038f),
				S(0.026f, 0.062f, -0.095f), S(-0.026f, 0.062f, -0.095f));

			mesh.Bone = WingLeft;
			mesh.Panel(colors.Wing, colors.Wing, Vector3.up, wingLeft + S(0f, 0f, 0.025f),
				wingLeft + S(-0.075f, 0.004f, -0.002f), wingLeft + S(-0.088f, 0.004f, -0.035f), wingLeft + S(0f, 0f, -0.03f));
			mesh.Bone = WingRight;
			mesh.Panel(colors.Wing, colors.Wing, Vector3.up, wingRight + S(0f, 0f, 0.025f),
				wingRight + S(0.075f, 0.004f, -0.002f), wingRight + S(0.088f, 0.004f, -0.035f), wingRight + S(0f, 0f, -0.03f));

			return Save(name, mesh, material, wingLeft, wingRight, S(0f, 0.065f, 0.035f), S(0f, 0.05f, -0.04f));
		}

		/// <summary>A soaring bird of prey, 1.2 m across the wings, centred on the origin.</summary>
		private static ParkCreature Buzzard(ParkPalette palette, Material material)
		{
			ParkMeshBuilder mesh = new ParkMeshBuilder(palette);
			Vector3 wingLeft = new Vector3(-0.05f, 0.02f, 0.02f), wingRight = new Vector3(0.05f, 0.022f, 0.02f);

			mesh.Bone = Root;
			mesh.Frustum(new Vector3(0f, 0.01f, -0.2f), new Vector2(0.08f, 0.07f), new Vector3(0f, 0.02f, 0.17f),
				new Vector2(0.12f, 0.11f), "7a5c3e", "6b4a33", "b7a57e", "b7a57e", "7a5c3e");

			mesh.Bone = Head;
			mesh.Box(new Vector3(0f, 0.04f, 0.22f), new Vector3(0.08f, 0.075f, 0.08f), "7a5c3e", "6b4a33", "cbc27c",
				"cbc27c", "7a5c3e");
			mesh.Pyramid(new Vector3(0f, 0.035f, 0.26f), new Vector2(0.03f, 0.03f), new Vector3(0f, 0.018f, 0.3f), "3b3631");
			mesh.Box(new Vector3(-0.041f, 0.05f, 0.235f), new Vector3(0.01f, 0.01f, 0.01f), "ffe3a0");
			mesh.Box(new Vector3(0.041f, 0.05f, 0.235f), new Vector3(0.01f, 0.01f, 0.01f), "ffe3a0");

			mesh.Bone = Tail;
			mesh.Panel("9c6d45", "cda872", Vector3.up, new Vector3(-0.05f, 0.01f, -0.18f), new Vector3(0.05f, 0.01f, -0.18f),
				new Vector3(0.11f, 0f, -0.38f), new Vector3(-0.11f, 0f, -0.38f));

			mesh.Bone = WingLeft;
			mesh.Panel("6b4a33", "b7a57e", Vector3.up, wingLeft + new Vector3(0f, 0f, 0.12f),
				wingLeft + new Vector3(-0.55f, 0.03f, 0.06f), wingLeft + new Vector3(-0.6f, 0.03f, -0.1f),
				wingLeft + new Vector3(0f, 0f, -0.14f));
			mesh.Bone = WingRight;
			mesh.Panel("6b4a33", "b7a57e", Vector3.up, wingRight + new Vector3(0f, 0f, 0.12f),
				wingRight + new Vector3(0.55f, 0.03f, 0.06f), wingRight + new Vector3(0.6f, 0.03f, -0.1f),
				wingRight + new Vector3(0f, 0f, -0.14f));

			return Save("Park_Life_Buzzard", mesh, material, wingLeft, wingRight, new Vector3(0f, 0.03f, 0.17f),
				new Vector3(0f, 0.01f, -0.18f));
		}

		/// <summary>A mallard floating with its waterline at the origin. The head bone dips the neck when dabbling.</summary>
		private static ParkCreature Duck(ParkPalette palette, Material material, string name, bool isDrake)
		{
			string body = isDrake ? "9c9c94" : "9c6d45", under = isDrake ? "aeb3b8" : "7f6b4b";
			string chest = isDrake ? "8e4a3c" : "7a5c3e", neck = isDrake ? "2e6b45" : "9c6d45";
			string head = isDrake ? "2e6b45" : "7a5c3e", collar = isDrake ? "f4f1ea" : "9c6d45";
			string bill = isDrake ? "f2c230" : "ee9a3a", wing = isDrake ? "8e949a" : "9c6d45";
			string wingUnder = isDrake ? "aeb3b8" : "7f6b4b", tailTop = isDrake ? "22303b" : "7a5c3e";
			string tailUnder = isDrake ? "f4f1ea" : "9c6d45";
			ParkMeshBuilder mesh = new ParkMeshBuilder(palette);
			Vector3 wingLeft = new Vector3(-0.075f, 0.07f, 0f), wingRight = new Vector3(0.075f, 0.072f, 0f);

			mesh.Bone = Root;
			mesh.Frustum(new Vector3(0f, 0.035f, -0.17f), new Vector2(0.12f, 0.09f), new Vector3(0f, 0.03f, 0.13f),
				new Vector2(0.17f, 0.13f), body, body, under, chest, tailUnder);
			mesh.Box(new Vector3(0f, 0.03f, 0.135f), new Vector3(0.15f, 0.11f, 0.03f), chest);

			mesh.Bone = Head;
			mesh.Box(new Vector3(0f, 0.12f, 0.13f), new Vector3(0.05f, 0.08f, 0.05f), neck);
			mesh.Box(new Vector3(0f, 0.095f, 0.13f), new Vector3(0.056f, 0.012f, 0.056f), collar);
			mesh.Box(new Vector3(0f, 0.175f, 0.145f), new Vector3(0.07f, 0.065f, 0.085f), head);
			mesh.Box(new Vector3(0f, 0.163f, 0.21f), new Vector3(0.036f, 0.016f, 0.06f), bill);
			mesh.Box(new Vector3(-0.036f, 0.185f, 0.16f), new Vector3(0.008f, 0.008f, 0.008f), Eye);
			mesh.Box(new Vector3(0.036f, 0.185f, 0.16f), new Vector3(0.008f, 0.008f, 0.008f), Eye);

			mesh.Bone = Tail;
			mesh.Panel(tailTop, tailUnder, Vector3.up, new Vector3(-0.04f, 0.07f, -0.16f), new Vector3(0.04f, 0.07f, -0.16f),
				new Vector3(0.02f, 0.11f, -0.22f), new Vector3(-0.02f, 0.11f, -0.22f));

			mesh.Bone = WingLeft;
			mesh.Panel(wing, wingUnder, Vector3.up, wingLeft + new Vector3(0f, 0f, 0.07f),
				wingLeft + new Vector3(-0.3f, 0.01f, 0.02f), wingLeft + new Vector3(-0.32f, 0.01f, -0.06f),
				wingLeft + new Vector3(0f, 0f, -0.08f));
			mesh.Bone = WingRight;
			mesh.Panel(wing, wingUnder, Vector3.up, wingRight + new Vector3(0f, 0f, 0.07f),
				wingRight + new Vector3(0.3f, 0.01f, 0.02f), wingRight + new Vector3(0.32f, 0.01f, -0.06f),
				wingRight + new Vector3(0f, 0f, -0.08f));

			return Save(name, mesh, material, wingLeft, wingRight, new Vector3(0f, 0.08f, 0.12f),
				new Vector3(0f, 0.06f, -0.16f));
		}

		/// <summary>A frog sitting at the origin, about 8 cm long.</summary>
		private static ParkCreature Frog(ParkPalette palette, Material material)
		{
			ParkMeshBuilder mesh = new ParkMeshBuilder(palette);
			mesh.Bone = Root;
			mesh.Frustum(new Vector3(0f, 0.024f, -0.028f), new Vector2(0.05f, 0.034f), new Vector3(0f, 0.026f, 0.02f),
				new Vector2(0.042f, 0.03f), "5e9f4a", "5e9f4a", "cbc27c", "5e9f4a", "4b8a44");
			mesh.Box(new Vector3(0f, 0.027f, 0.04f), new Vector3(0.044f, 0.024f, 0.028f), "5e9f4a", "5e9f4a", "cbc27c");
			mesh.Box(new Vector3(-0.016f, 0.042f, 0.046f), new Vector3(0.012f, 0.012f, 0.012f), "86bd5c");
			mesh.Box(new Vector3(0.016f, 0.042f, 0.046f), new Vector3(0.012f, 0.012f, 0.012f), "86bd5c");
			mesh.Box(new Vector3(-0.016f, 0.044f, 0.052f), new Vector3(0.005f, 0.005f, 0.004f), Eye);
			mesh.Box(new Vector3(0.016f, 0.044f, 0.052f), new Vector3(0.005f, 0.005f, 0.004f), Eye);
			mesh.Box(new Vector3(-0.032f, 0.01f, -0.02f), new Vector3(0.018f, 0.02f, 0.045f), "4b8a44");
			mesh.Box(new Vector3(0.032f, 0.01f, -0.02f), new Vector3(0.018f, 0.02f, 0.045f), "4b8a44");
			mesh.Box(new Vector3(-0.022f, 0.008f, 0.03f), new Vector3(0.008f, 0.016f, 0.008f), "4b8a44");
			mesh.Box(new Vector3(0.022f, 0.008f, 0.03f), new Vector3(0.008f, 0.016f, 0.008f), "4b8a44");
			return Save("Park_Life_Frog", mesh, material, null, null, null, null);
		}

		/// <summary>A pond fish centred on the origin, its tail hinged to swish.</summary>
		private static ParkCreature Fish(ParkPalette palette, Material material)
		{
			ParkMeshBuilder mesh = new ParkMeshBuilder(palette);
			mesh.Bone = Root;
			mesh.Frustum(Vector3.zero, new Vector2(0.05f, 0.07f), new Vector3(0f, 0f, 0.1f), new Vector2(0.006f, 0.01f),
				"f08a3c", "ee9a3a", "f6c08e", "ee9a3a", "f08a3c");
			mesh.Frustum(new Vector3(0f, 0f, -0.09f), new Vector2(0.008f, 0.012f), Vector3.zero, new Vector2(0.05f, 0.07f),
				"f08a3c", "ee9a3a", "f6c08e", "f08a3c", "f08a3c");
			mesh.Panel("ee9a3a", "ee9a3a", Vector3.right, new Vector3(0f, 0.035f, 0.03f), new Vector3(0f, 0.035f, -0.04f),
				new Vector3(0f, 0.06f, -0.03f));
			mesh.Bone = Tail;
			mesh.Panel("ee9a3a", "ee9a3a", Vector3.right, new Vector3(0f, 0f, -0.09f), new Vector3(0f, 0.045f, -0.15f),
				new Vector3(0f, -0.045f, -0.15f));
			return Save("Park_Life_Fish", mesh, material, null, null, null, new Vector3(0f, 0f, -0.09f));
		}

		/// <summary>A bumblebee, about 3 cm long, centred on the origin.</summary>
		private static ParkCreature Bee(ParkPalette palette, Material material)
		{
			ParkMeshBuilder mesh = new ParkMeshBuilder(palette);
			Vector3 wingLeft = new Vector3(-0.004f, 0.006f, 0.006f), wingRight = new Vector3(0.004f, 0.0065f, 0.006f);
			mesh.Bone = Root;
			mesh.Box(new Vector3(0f, 0f, -0.007f), new Vector3(0.013f, 0.012f, 0.016f), "edd244");
			mesh.Box(new Vector3(0f, 0f, -0.008f), new Vector3(0.0136f, 0.0126f, 0.004f), Eye);
			mesh.Box(new Vector3(0f, 0.001f, 0.006f), new Vector3(0.011f, 0.011f, 0.01f), "2c3035");
			mesh.Box(new Vector3(0f, 0f, 0.0145f), new Vector3(0.008f, 0.008f, 0.007f), Eye);
			mesh.Bone = WingLeft;
			mesh.Panel("e8ecef", "e6f8ff", Vector3.up, wingLeft + new Vector3(0f, 0f, 0.004f),
				wingLeft + new Vector3(-0.014f, 0.002f, 0.002f), wingLeft + new Vector3(-0.014f, 0.002f, -0.005f),
				wingLeft + new Vector3(0f, 0f, -0.004f));
			mesh.Bone = WingRight;
			mesh.Panel("e8ecef", "e6f8ff", Vector3.up, wingRight + new Vector3(0f, 0f, 0.004f),
				wingRight + new Vector3(0.014f, 0.002f, 0.002f), wingRight + new Vector3(0.014f, 0.002f, -0.005f),
				wingRight + new Vector3(0f, 0f, -0.004f));
			return Save("Park_Life_Bee", mesh, material, wingLeft, wingRight, null, null);
		}

		/// <summary>A butterfly with fore and hind wings in one colour and darker wing tips, centred on the origin.</summary>
		private static ParkCreature Butterfly(ParkPalette palette, Material material, string name, string wing, string tip)
		{
			ParkMeshBuilder mesh = new ParkMeshBuilder(palette);
			Vector3 wingLeft = new Vector3(-0.002f, 0f, 0f), wingRight = new Vector3(0.002f, 0.0005f, 0f);
			mesh.Bone = Root;
			mesh.Box(Vector3.zero, new Vector3(0.004f, 0.004f, 0.028f), Eye);
			foreach (int side in new[] { -1, 1 })
			{
				mesh.Bone = side < 0 ? WingLeft : WingRight;
				Vector3 pivot = side < 0 ? wingLeft : wingRight;
				Vector3 P(float x, float y, float z) => pivot + new Vector3(x * side, y, z);
				mesh.Panel(wing, wing, Vector3.up, P(0f, 0f, 0.012f), P(0.042f, 0f, 0.02f), P(0.038f, 0f, -0.004f),
					P(0f, 0f, -0.002f));
				mesh.Panel(wing, wing, Vector3.up, P(0f, 0f, -0.002f), P(0.03f, 0f, -0.008f), P(0.018f, 0f, -0.026f),
					P(0f, 0f, -0.014f));
				mesh.Panel(tip, tip, Vector3.up, P(0.03f, 0.0006f, 0.018f), P(0.042f, 0.0006f, 0.02f),
					P(0.039f, 0.0006f, 0.008f));
			}

			return Save(name, mesh, material, wingLeft, wingRight, null, null);
		}

		/// <summary>A dragonfly with two pairs of long clear wings, centred on the origin.</summary>
		private static ParkCreature Dragonfly(ParkPalette palette, Material material)
		{
			ParkMeshBuilder mesh = new ParkMeshBuilder(palette);
			Vector3 wingLeft = new Vector3(-0.004f, 0.004f, 0.006f), wingRight = new Vector3(0.004f, 0.0045f, 0.006f);
			mesh.Bone = Root;
			mesh.Box(new Vector3(0f, 0f, -0.025f), new Vector3(0.005f, 0.005f, 0.05f), "3e73bf");
			mesh.Box(new Vector3(0f, 0.001f, 0.006f), new Vector3(0.009f, 0.009f, 0.014f), "2c3f66");
			mesh.Box(new Vector3(0f, 0.001f, 0.017f), new Vector3(0.011f, 0.009f, 0.008f), "79d6f2");
			foreach (int side in new[] { -1, 1 })
			{
				mesh.Bone = side < 0 ? WingLeft : WingRight;
				Vector3 pivot = side < 0 ? wingLeft : wingRight;
				Vector3 P(float x, float y, float z) => pivot + new Vector3(x * side, y, z);
				mesh.Panel("e6f8ff", "e8ecef", Vector3.up, P(0f, 0f, 0.006f), P(0.045f, 0.002f, 0.008f),
					P(0.045f, 0.002f, 0f), P(0f, 0f, 0f));
				mesh.Panel("e6f8ff", "e8ecef", Vector3.up, P(0f, 0f, -0.002f), P(0.042f, 0.002f, -0.001f),
					P(0.042f, 0.002f, -0.009f), P(0f, 0f, -0.008f));
			}

			return Save("Park_Life_Dragonfly", mesh, material, wingLeft, wingRight, null, null);
		}

		/// <summary>
		/// Turns the built faces into a skinned mesh asset and a prefab: the root plus a bone for each part the model has,
		/// bound where the parts were modelled.
		/// </summary>
		private static ParkCreature Save(string name, ParkMeshBuilder builder, Material material, Vector3? wingLeft,
			Vector3? wingRight, Vector3? head, Vector3? tail)
		{
			Vector3?[] pivots = { Vector3.zero, wingLeft, wingRight, head, tail };
			GameObject root = new GameObject(name);
			Transform[] bones = new Transform[pivots.Length];
			bones[Root] = root.transform;
			for (int i = 1; i < pivots.Length; i++)
			{
				GameObject bone = new GameObject(BoneNames[i]);
				bone.transform.SetParent(root.transform, false);
				bone.transform.localPosition = pivots[i] ?? Vector3.zero;
				bones[i] = bone.transform;
			}

			Matrix4x4[] bindposes = bones.Select(bone => bone.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
			string meshPath = $"{ModelsFolder}/{name}.asset";
			Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
			if (mesh == null)
			{
				mesh = builder.Build(new Mesh(), name, bindposes);
				AssetDatabase.CreateAsset(mesh, meshPath);
			}
			else
			{
				builder.Build(mesh, name, bindposes);
				EditorUtility.SetDirty(mesh);
			}

			SkinnedMeshRenderer skinned = root.AddComponent<SkinnedMeshRenderer>();
			skinned.sharedMesh = mesh;
			skinned.bones = bones;
			skinned.rootBone = root.transform;
			skinned.sharedMaterial = material;
			skinned.localBounds = new Bounds(mesh.bounds.center, mesh.bounds.size * BoundsMargin);
			skinned.shadowCastingMode = ShadowCastingMode.Off;
			skinned.quality = SkinQuality.Bone1;
			skinned.updateWhenOffscreen = false;
			skinned.skinnedMotionVectors = false;

			ParkCreature creature = root.AddComponent<ParkCreature>();
			creature.Configure(skinned, wingLeft.HasValue ? bones[WingLeft] : null,
				wingRight.HasValue ? bones[WingRight] : null, head.HasValue ? bones[Head] : null,
				tail.HasValue ? bones[Tail] : null);
			GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabsFolder}/{name}.prefab");
			Object.DestroyImmediate(root);
			return prefab.GetComponent<ParkCreature>();
		}

		#endregion
	}
}
