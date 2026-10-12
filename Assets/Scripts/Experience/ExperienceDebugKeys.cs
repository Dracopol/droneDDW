using UnityEngine;
using UnityEngine.InputSystem;

namespace DroneDDW.Experience
{
	// Editor only, for trying the flow without doing every step: N ends the line or step underway as if it were done
	// (the Neighborhood's own keys, 1-3, arrows, I, F, R and S, still work there)
	[DisallowMultipleComponent]
	[AddComponentMenu("DroneDDW/Experience/Experience Debug Keys")]
	internal sealed class ExperienceDebugKeys : MonoBehaviour
	{
		#region Serialized fields

		[SerializeField] private ExperienceDirector _director;

		#endregion

		#region Update

		private void Update()
		{
			if (!Application.isEditor || Keyboard.current == null)
			{
				return;
			}

			if (Keyboard.current.nKey.wasPressedThisFrame)
			{
				_director.Skip();
			}
		}

		#endregion

		#region Internal

		internal void Configure(ExperienceDirector director)
		{
			_director = director;
		}

		#endregion
	}
}
