using DroneDDW.NarratorOrb.Rendering;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.NarratorOrb.Editor
{
	// The particle count scale and a Bake button, which regenerates the meshes in place
	[CustomEditor(typeof(NarratorOrbLayout))]
	internal sealed class NarratorOrbLayoutEditor : UnityEditor.Editor
	{
		#region Public

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			NarratorOrbLayout layout = (NarratorOrbLayout)target;
			EditorGUILayout.Space();
			string baked = layout.Particles == null
				? "Not baked yet."
				: $"Baked {layout.BakedParticleCount:N0} of {NarratorOrbParticleGenerator.TotalCount:N0} particles "
					+ $"(scale {layout.BakedCountScale:0.##}): {layout.Particles.vertexCount:N0} vertices.";
			EditorGUILayout.HelpBox(baked, MessageType.Info);

			using (new EditorGUI.DisabledScope(Application.isPlaying))
			{
				if (GUILayout.Button("Bake"))
				{
					NarratorOrbAssetBuilder.BakeLayout(layout);
				}
			}
		}

		#endregion
	}
}
