using System.Collections.Generic;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>The materials the builder made: by FBX material name (for remapping) and the special ones.</summary>
    internal sealed class NeighborhoodMaterialSet
    {
        #region Properties

        /// <summary>Street_Mat_* and Street_Char_* by name, as the FBX files call them.</summary>
        public Dictionary<string, Material> ByName { get; } = new Dictionary<string, Material>();

        /// <summary>Light pool material per light kind (Warm, Led).</summary>
        public Dictionary<string, Material> LightPools { get; } = new Dictionary<string, Material>();

        public Material Leash { get; set; }
        public Material Skybox { get; set; }

        #endregion
    }
}
