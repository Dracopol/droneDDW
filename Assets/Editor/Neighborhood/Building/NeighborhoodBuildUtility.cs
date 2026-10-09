using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>Editor-time helpers for the builders: instancing the FBX models and finding the nodes they name.</summary>
    internal static class NeighborhoodBuildUtility
    {
        #region Internal

        /// <summary>An unpacked copy of a model, so components can be added to its nodes freely.</summary>
        internal static GameObject InstantiateUnpacked(GameObject model, Transform parent, string name)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = name;
            return instance;
        }

        /// <summary>Editor-time lookup of a node the Blender export named.</summary>
        internal static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }

                Transform found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>Every node under a root by name (names in the export are unique).</summary>
        internal static Dictionary<string, Transform> IndexDeep(Transform root)
        {
            Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
            Collect(root, nodes);
            return nodes;
        }

        #endregion

        #region Private

        private static void Collect(Transform parent, Dictionary<string, Transform> nodes)
        {
            foreach (Transform child in parent)
            {
                nodes[child.name] = child;
                Collect(child, nodes);
            }
        }

        #endregion
    }
}
