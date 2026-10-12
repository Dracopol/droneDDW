using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDDW.Neighborhood.Experience
{
    /// <summary>
    /// Switches on the scene's own copy of the XR rig only when the Neighborhood is opened on its own, to test it. The
    /// experience loads the Neighborhood on top of its own scene, which brings the rig, the admin menu and the session,
    /// so the copy stays off there: two rigs would mean two cameras, two OVRManagers and every hand twice. The builder
    /// saves the copy switched off, so nothing in it wakes up before this decides.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Neighborhood/Neighborhood Standalone Rig")]
    public sealed class NeighborhoodStandaloneRig : MonoBehaviour
    {
        #region Serialized fields

        [SerializeField, Tooltip("The root of the scene's rig copy, saved switched off.")]
        private GameObject _rig;

        #endregion

        #region Setup

        private void Awake()
        {
            if (_rig == null)
            {
                return;
            }

            // Loaded on top of the experience, there are two scenes
            bool isStandalone = SceneManager.sceneCount == 1;
            _rig.SetActive(isStandalone);
            if (isStandalone)
            {
                Debug.Log("[Neighborhood] Opened on its own: using the scene's own XR rig.", this);
            }
        }

        #endregion

        #region Internal

        internal void Configure(GameObject rig)
        {
            _rig = rig;
        }

        #endregion
    }
}
