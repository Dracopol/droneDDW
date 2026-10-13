using System;
using System.Collections.Generic;
using System.IO;
using DroneDDW.Neighborhood.Characters;
using DroneDDW.Neighborhood.Editor.Layout;
using DroneDDW.Neighborhood.Versions;
using DroneDDW.Neighborhood.Voice;
using UnityEditor;
using UnityEngine;

namespace DroneDDW.Neighborhood.Editor.Building
{
    /// <summary>
    /// Creates or updates Voice/Neighborhood_VoiceLibrary.asset from the layout's copy of the voice script and links
    /// every AudioClip in the project whose file name is a line ID (GRA_01.wav and so on). Clips linked by hand,
    /// emotions and reaction sets edited in the asset are kept, unless the layout carries a newer version of the script:
    /// then the reaction sets go back to its defaults and every clip is linked again by name, because IDs may have
    /// moved. Menu: DroneDDW / Neighborhood / Link Voice Lines.
    /// </summary>
    internal static class NeighborhoodVoiceLibraryBuilder
    {
        #region Fields

        private const string MenuPath = "DroneDDW/Neighborhood/Link Voice Lines";
        private const string LibraryFileName = "Neighborhood_VoiceLibrary.asset";
        private const string LayoutFileName = "Street_Layout.json";

        #endregion

        #region Internal

        internal static NeighborhoodVoiceLibrary CreateOrUpdate(string root, NeighborhoodLayout layout)
        {
            string folder = NeighborhoodAssetUtility.EnsureFolder(root, NeighborhoodAssetUtility.VoiceFolder);
            string path = $"{folder}/{LibraryFileName}";
            NeighborhoodVoiceLibrary library = AssetDatabase.LoadAssetAtPath<NeighborhoodVoiceLibrary>(path);
            bool isNew = library == null;
            if (isNew)
            {
                library = ScriptableObject.CreateInstance<NeighborhoodVoiceLibrary>();
                AssetDatabase.CreateAsset(library, path);
            }

            bool isNewScript = !isNew && library.ScriptVersion != layout.ScriptVersion;
            if (isNewScript)
            {
                Debug.LogWarning($"[Neighborhood] The voice script changed (\"{library.ScriptVersion}\" to \"{layout.ScriptVersion}\"): "
                    + "the reaction sets are back to their defaults and every WAV is linked again by name.", library);
            }

            Dictionary<string, AudioClip> kept = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, NeighborhoodEmotion> keptEmotions = new Dictionary<string, NeighborhoodEmotion>(StringComparer.OrdinalIgnoreCase);
            foreach (NeighborhoodVoiceLine line in library.Lines)
            {
                if (!isNewScript && line != null)
                {
                    keptEmotions[line.Id] = line.Emotion;
                }

                if (!isNewScript && line != null && line.Clip != null)
                {
                    kept[line.Id] = line.Clip;
                }
            }

            List<NeighborhoodVoiceLine> lines = new List<NeighborhoodVoiceLine>();
            int linked = 0;
            foreach (NeighborhoodLayoutVoiceLine entry in layout.VoiceLines)
            {
                AudioClip clip = kept.TryGetValue(entry.Id, out AudioClip existing) ? existing : FindClip(entry.Id);
                if (clip != null)
                {
                    linked++;
                }

                NeighborhoodEmotion emotion = keptEmotions.TryGetValue(entry.Id, out NeighborhoodEmotion edited)
                    ? edited
                    : (NeighborhoodEmotion)entry.Emotion;
                lines.Add(new NeighborhoodVoiceLine(entry.Id, (NeighborhoodCharacterId)entry.Character, emotion,
                    (NeighborhoodVoiceSide)entry.Side, (NeighborhoodVoiceStage)entry.Stage, entry.Trigger, entry.Text, clip));
            }

            List<NeighborhoodReactionSet> reactions = new List<NeighborhoodReactionSet>();
            if (isNew || isNewScript || library.Reactions.Count == 0)
            {
                foreach (NeighborhoodLayoutReaction entry in layout.Reactions)
                {
                    string[] ids = new string[entry.Lines.Count];
                    for (int i = 0; i < ids.Length; i++)
                    {
                        ids[i] = entry.Lines[i];
                    }

                    reactions.Add(new NeighborhoodReactionSet(NeighborhoodVersions.Clamp(entry.Version), (NeighborhoodVoiceStage)entry.Stage, ids));
                }
            }
            else
            {
                reactions.AddRange(library.Reactions);
            }

            library.Configure(layout.ScriptVersion, lines, reactions);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Neighborhood] Voice library: {linked} of {lines.Count} lines have their WAV.", library);
            return library;
        }

        #endregion

        #region Private

        [MenuItem(MenuPath)]
        private static void LinkVoiceLines()
        {
            string root = NeighborhoodAssetUtility.FindRootFolder();
            TextAsset json = string.IsNullOrEmpty(root)
                ? null
                : AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{NeighborhoodAssetUtility.LayoutsFolder}/{LayoutFileName}");
            if (json == null)
            {
                EditorUtility.DisplayDialog("Neighborhood", "Could not find the Neighborhood content folder (Street_Palette.png and Street_Layout.json).", "OK");
                return;
            }

            Selection.activeObject = CreateOrUpdate(root, JsonUtility.FromJson<NeighborhoodLayout>(json.text));
        }

        /// <summary>The clip whose file name is the ID. With several (two languages, a copy), the first by path wins.</summary>
        private static AudioClip FindClip(string id)
        {
            List<string> matches = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets($"{id} t:AudioClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), id, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(path);
                }
            }

            if (matches.Count == 0)
            {
                return null;
            }

            matches.Sort(StringComparer.Ordinal);
            if (matches.Count > 1)
            {
                Debug.LogWarning($"[Neighborhood] {matches.Count} WAVs are called {id}; using {matches[0]}. "
                    + "Link the one you want in the voice library by hand, or keep one language in the project.");
            }

            return AssetDatabase.LoadAssetAtPath<AudioClip>(matches[0]);
        }

        #endregion
    }
}
