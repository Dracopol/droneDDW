using System;
using System.Collections.Generic;
using System.Text;
using DroneDDW.Parks.Levels;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DroneDDW.Parks.Table
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)]
    [AddComponentMenu("DroneDDW/Parks/Park Time World")]
    public sealed class ParkTimeWorld : MonoBehaviour
    {
        #region Entities

        private enum Category
        {
            None,
            Tree,
            Shrub,
            Plant,
            Fauna,
        }

        private enum TechKind
        {
            Station,
            Sensor,
            Tower,
            Drone,
            Overlay,
            Zone,
            Path,
            Pollinator,
        }

        private struct Slot
        {
            public Renderer Renderer;
            public int Index;
            public int ColorId;
            public Color Base;
            public bool IsTintable;
        }

        private sealed class Item
        {
            public Transform T;
            public GameObject Go;
            public Category Cat;
            public Vector3 BaseScale;
            public int Rank;
            public int GroupCount;
            public float R;
            public Color Blossom;
            public bool IsClone;
            public readonly List<Slot> Slots = new List<Slot>();
            public float AppliedBlossom = -1f;
            public float AppliedTidy = -1f;
        }

        private sealed class Tech
        {
            public GameObject Go;
            public Transform T;
            public TechKind Kind;
            public Vector3 BaseScale;
            public int Rank;
            public int GroupCount;
            public float S;
            public Vector3 Spot;
            public float Phase;
            public float Speed;
            public float Radius;
            public Transform[] Rotors;
        }

        [Serializable]
        private struct Look
        {
            public Color FogColor;
            public float FogDensity;
            public Color SunColor;
            public float SunIntensity;
            public Color AmbientColor;
        }

        #endregion

        #region Serialized fields

        [Header("References (all found automatically when empty)")]
        [SerializeField, Tooltip("Where to look for the environment. Empty = every root object of the scene.")]
        private Transform _root;

        [SerializeField] private ParkTimeController _timeController;
        [SerializeField] private ParkLevelController _levelController;
        [SerializeField, Tooltip("The Nature-Efficiency slider. Found as the slider that is not the time slider.")]
        private ParkLevelSlider _levelSlider;

        [SerializeField, Range(0, 4), Tooltip("Level used if nothing announces one in the first second.")]
        private int _startLevel = 2;

        [SerializeField] private Light _sun;

        [Header("Finding the environment (words in object names, any capitals)")]
        [SerializeField]
        private string[] _treeWords =
            { "tree", "pine", "oak", "birch", "beech", "palm", "willow", "spruce", "conifer", "cedar", "poplar", "maple" };

        [SerializeField] private string[] _shrubWords = { "bush", "shrub", "hedge", "bramble", "thicket" };

        [SerializeField]
        private string[] _plantWords =
        {
            "grass", "flower", "plant", "fern", "weed", "reed", "clover", "lily", "daisy", "tulip", "moss", "herb",
            "foliage", "vegetation", "wildflower", "bloom", "meadow",
        };

        [SerializeField]
        private string[] _faunaWords =
        {
            "bird", "insect", "butterfly", "bee", "deer", "rabbit", "fox", "duck", "fish", "frog", "animal", "squirrel",
            "crow", "owl", "hawk", "eagle", "pigeon", "sparrow", "hare", "wildlife",
        };

        [SerializeField, Tooltip("Whole words that are never touched (the XR rig, the ground, the board...).")]
        private string[] _ignoreWords =
        {
            "xr", "origin", "rig", "hand", "hands", "controller", "camera", "terrain", "floor", "sky", "canvas", "ui",
            "board", "table", "slider", "hud",
        };

        [SerializeField, Min(2), Tooltip("A named object with at least this many named children is a group (a 'Trees' "
            + "parent) and its children are the individual items. Lower it if single trees are skipped, raise it if a "
            + "tree's trunk and leaves are treated as separate trees.")]
        private int _minGroupChildren = 3;

        [SerializeField, Tooltip("Also colours materials whose base colour is white. Switch on if no tree or plant "
            + "changes colour (the console tells how many materials can be coloured).")]
        private bool _tintWhiteMaterials;

        [SerializeField] private int _seed = 7;

        [Header("Nature end: new biodiversity")]
        [SerializeField, Min(0f), Tooltip("New trees as a share of the existing ones.")]
        private float _newTreeShare = 0.5f;

        [SerializeField, Min(0f)] private float _newShrubShare = 1f;
        [SerializeField, Min(0f)] private float _newPlantShare = 1.5f;
        [SerializeField, Min(0f)] private float _newFaunaShare = 0.5f;

        [SerializeField, Min(0), Tooltip("Limit of new items in total. Keep it low for VR.")]
        private int _maxNewItems = 250;

        [SerializeField, Min(0.5f), Tooltip("How far from its original a new item grows, in metres: min and max.")]
        private Vector2 _newItemDistance = new Vector2(1f, 4f);

        [SerializeField, Tooltip("Blossom and flower colours the new species and colourful trees take.")]
        private Color[] _blossomColors =
        {
            new Color(0.95f, 0.55f, 0.70f), new Color(0.95f, 0.60f, 0.20f), new Color(0.95f, 0.85f, 0.25f),
            new Color(0.60f, 0.40f, 0.80f), new Color(0.15f, 0.55f, 0.40f), new Color(0.85f, 0.25f, 0.25f),
            new Color(0.55f, 0.80f, 0.35f),
        };

        [SerializeField, Min(0), Tooltip("Generated pollinators at their maximum.")]
        private int _pollinatorCount = 60;

        [Header("Efficiency end: management and infrastructure")]
        [SerializeField] private Color _tidyColor = new Color(0.42f, 0.62f, 0.30f);
        [SerializeField, Min(0)] private int _stationCount = 6;
        [SerializeField, Min(0)] private int _sensorCount = 20;
        [SerializeField, Min(0)] private int _towerCount = 4;
        [SerializeField, Min(0)] private int _droneCount = 20;
        [SerializeField, Min(0)] private int _zoneCount = 8;

        [SerializeField, Tooltip("Drone height above the ground: at the Nature end and at the Efficiency end.")]
        private Vector2 _droneHeight = new Vector2(14f, 4f);

        [SerializeField, Tooltip("Drone patrol radius: at the Nature end and at the Efficiency end.")]
        private Vector2 _droneRadius = new Vector2(8f, 4f);

        [SerializeField, Min(0f), Tooltip("Generated things keep this far from trees and shrubs.")]
        private float _clearanceMeters = 1.8f;

        [SerializeField, Tooltip("Use the area below instead of the area of the found items.")]
        private bool _useCustomArea;

        [SerializeField] private Bounds _customArea = new Bounds(Vector3.zero, new Vector3(40f, 10f, 40f));

        [Header("Light: Nature end and Efficiency end, at 2026, 2050 and 2100")]
        [SerializeField]
        private Look[] _natureLook =
        {
            new Look
            {
                FogColor = new Color(0.78f, 0.87f, 0.90f), FogDensity = 0.0015f, SunColor = new Color(1f, 0.97f, 0.90f),
                SunIntensity = 1f, AmbientColor = new Color(0.55f, 0.62f, 0.60f),
            },
            new Look
            {
                FogColor = new Color(0.76f, 0.88f, 0.86f), FogDensity = 0.0018f, SunColor = new Color(1f, 0.96f, 0.86f),
                SunIntensity = 1f, AmbientColor = new Color(0.53f, 0.64f, 0.58f),
            },
            new Look
            {
                FogColor = new Color(0.74f, 0.89f, 0.84f), FogDensity = 0.0022f, SunColor = new Color(1f, 0.95f, 0.84f),
                SunIntensity = 1f, AmbientColor = new Color(0.50f, 0.65f, 0.56f),
            },
        };

        [SerializeField]
        private Look[] _efficiencyLook =
        {
            new Look
            {
                FogColor = new Color(0.78f, 0.87f, 0.92f), FogDensity = 0.0016f, SunColor = new Color(1f, 0.98f, 0.93f),
                SunIntensity = 1f, AmbientColor = new Color(0.56f, 0.62f, 0.63f),
            },
            new Look
            {
                FogColor = new Color(0.82f, 0.87f, 0.90f), FogDensity = 0.0012f, SunColor = new Color(0.99f, 0.99f, 0.96f),
                SunIntensity = 1.02f, AmbientColor = new Color(0.58f, 0.62f, 0.66f),
            },
            new Look
            {
                FogColor = new Color(0.86f, 0.88f, 0.90f), FogDensity = 0.0008f, SunColor = new Color(0.97f, 0.99f, 1f),
                SunIntensity = 1.05f, AmbientColor = new Color(0.60f, 0.63f, 0.68f),
            },
        };

        [SerializeField, Min(0f), Tooltip("Seconds the world takes to follow a level change.")]
        private float _levelBlendSeconds = 2f;

        #endregion

        #region Fields

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<Item> _items = new List<Item>();
        private readonly List<Tech> _tech = new List<Tech>();
        private readonly List<string> _tokens = new List<string>();
        private readonly List<Vector3> _avoid = new List<Vector3>();
        private readonly List<Vector3> _anchors = new List<Vector3>();
        private MaterialPropertyBlock _block;
        private Transform _boardRoot;
        private Transform _generatedRoot;
        private Bounds _area;
        private Shader _shader;
        private Material _padMaterial;
        private Material _darkMaterial;
        private Material _glowMaterial;
        private Material _zoneMaterial;
        private Material _pathMaterial;
        private Material[] _pollinatorMaterials;
        private Mesh _ringMesh;
        private bool _isReady;
        private bool _hasLevel;
        private bool _isDirty = true;
        private float _target;
        private float _efficiency;
        private float _efficiencyVelocity;
        private float _appliedTime = -1f;
        private float _appliedEfficiency = -1f;

        #endregion

        #region Properties

        public float TimePosition => _timeController != null ? _timeController.Progress : 0f;

        public float Efficiency => _efficiency;

        public float Impact => GetImpact(TimePosition);

        #endregion

        #region Setup

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_timeController == null)
            {
                _timeController = FindFirst<ParkTimeController>();
            }

            if (_levelController == null)
            {
                _levelController = FindFirst<ParkLevelController>();
            }

            if (_levelSlider == null)
            {
                ParkLevelSlider timeSlider = _timeController != null ? _timeController.Slider : null;
                foreach (ParkLevelSlider slider in FindAll<ParkLevelSlider>())
                {
                    if (slider != timeSlider)
                    {
                        _levelSlider = slider;
                        break;
                    }
                }
            }

            ParkBoardFollow board = FindFirst<ParkBoardFollow>();
            _boardRoot = board != null ? board.transform : null;
            if (_sun == null)
            {
                _sun = RenderSettings.sun;
            }

            if (_sun == null)
            {
                foreach (Light light in FindAll<Light>())
                {
                    if (light.type == LightType.Directional)
                    {
                        _sun = light;
                        break;
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (_levelController != null)
            {
                _levelController.OnLevelChange += HandleLevelChange;
            }

            if (_levelSlider != null)
            {
                _levelSlider.OnLevelCommit += HandleLevelCommit;
            }
        }

        private void OnDisable()
        {
            if (_levelController != null)
            {
                _levelController.OnLevelChange -= HandleLevelChange;
            }

            if (_levelSlider != null)
            {
                _levelSlider.OnLevelCommit -= HandleLevelCommit;
            }
        }

        private void Start()
        {
            if (_timeController == null)
            {
                Debug.LogError("[Parks] Park Time World needs a Park Time Controller in the scene.", this);
                enabled = false;
                return;
            }

            Discover();
            CreateClones();
            CreateTech();
            LogSummary();
            _isReady = true;
        }

        #endregion

        #region Update

        private void LateUpdate()
        {
            if (!_isReady)
            {
                return;
            }

            if (!_hasLevel)
            {
                if (_levelSlider != null && _levelSlider.Detent >= 0)
                {
                    SetLevel(_levelSlider.Detent, true);
                }
                else if (Time.time > 1f)
                {
                    SetLevel(_startLevel, true);
                }
            }

            if (_efficiency != _target)
            {
                _efficiency = _levelBlendSeconds > 0f
                    ? Mathf.SmoothDamp(_efficiency, _target, ref _efficiencyVelocity, _levelBlendSeconds * 0.4f)
                    : _target;
                if (Mathf.Abs(_efficiency - _target) < 0.0005f)
                {
                    _efficiency = _target;
                    _efficiencyVelocity = 0f;
                }
            }

            float time = TimePosition;
            if (_isDirty || !Mathf.Approximately(time, _appliedTime)
                || !Mathf.Approximately(_efficiency, _appliedEfficiency))
            {
                _isDirty = false;
                _appliedTime = time;
                _appliedEfficiency = _efficiency;
                float impact = GetImpact(time);
                ApplyLight(time, _efficiency);
                ApplyItems(impact, _efficiency);
                ApplyTech(impact, _efficiency);
            }

            Tick();
        }

        #endregion

        #region Event handlers

        private void HandleLevelChange(ParkLevelChange change)
        {
            SetLevel(change.Next, change.IsImmediate);
        }

        private void HandleLevelCommit(int detent)
        {
            SetLevel(detent, false);
        }

        #endregion

        #region Internal

        [ContextMenu("Log what was found")]
        private void LogSummary()
        {
            int[] counts = new int[5];
            int tintable = 0;
            int batched = 0;
            foreach (Item item in _items)
            {
                if (item.IsClone)
                {
                    continue;
                }

                counts[(int)item.Cat]++;
                foreach (Slot slot in item.Slots)
                {
                    if (slot.IsTintable)
                    {
                        tintable++;
                    }

                    if (slot.Renderer.isPartOfStaticBatch)
                    {
                        batched++;
                    }
                }
            }

            Debug.Log($"[Parks] Time world found {counts[1]} trees, {counts[2]} shrubs, {counts[3]} plants, "
                + $"{counts[4]} animals; {tintable} materials can change colour; created {CountClones()} new "
                + $"species and {_tech.Count} generated things.", this);
            if (counts[1] + counts[2] + counts[3] + counts[4] == 0)
            {
                Debug.LogWarning("[Parks] Nothing in the scene matched the name lists, so only the generated things "
                    + "and the light will change. Check the object names against the word lists on Park Time World.",
                    this);
            }

            if (tintable == 0)
            {
                Debug.LogWarning("[Parks] No material can change colour. Switch on 'Tint White Materials' if the "
                    + "foliage uses white base colours with textures.", this);
            }

            if (batched > 0)
            {
                Debug.LogWarning($"[Parks] {batched} renderers are part of a static batch, so they can't scale. Untick "
                    + "Static (Batching Static) on the environment objects.", this);
            }
        }

        #endregion

        #region Private: levels and light

        private void SetLevel(int level, bool immediate)
        {
            level = ParkLevels.Clamp(level);
            _target = level / (float)(ParkLevels.Count - 1);
            if (immediate || !_hasLevel)
            {
                _efficiency = _target;
                _efficiencyVelocity = 0f;
            }

            _hasLevel = true;
            _isDirty = true;
        }

        private static float GetImpact(float time)
        {
            float t = Mathf.Clamp(time, 0f, 2f);
            return t <= 1f ? Mathf.Lerp(0.15f, 0.55f, t) : Mathf.Lerp(0.55f, 1f, t - 1f);
        }

        private void ApplyLight(float time, float efficiency)
        {
            Look nature = Blend(_natureLook, time);
            Look efficient = Blend(_efficiencyLook, time);
            float e = Mathf.Clamp01(efficiency);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(nature.FogColor, efficient.FogColor, e);
            RenderSettings.fogDensity = Mathf.Lerp(nature.FogDensity, efficient.FogDensity, e);
            RenderSettings.ambientLight = Color.Lerp(nature.AmbientColor, efficient.AmbientColor, e);
            if (_sun != null)
            {
                _sun.color = Color.Lerp(nature.SunColor, efficient.SunColor, e);
                _sun.intensity = Mathf.Lerp(nature.SunIntensity, efficient.SunIntensity, e);
            }
        }

        private static Look Blend(Look[] looks, float time)
        {
            if (looks == null || looks.Length == 0)
            {
                return default;
            }

            if (looks.Length == 1)
            {
                return looks[0];
            }

            float t = Mathf.Clamp(time, 0f, looks.Length - 1);
            int lower = Mathf.Min((int)t, looks.Length - 2);
            float amount = t - lower;
            Look a = looks[lower];
            Look b = looks[lower + 1];
            return new Look
            {
                FogColor = Color.Lerp(a.FogColor, b.FogColor, amount),
                FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, amount),
                SunColor = Color.Lerp(a.SunColor, b.SunColor, amount),
                SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, amount),
                AmbientColor = Color.Lerp(a.AmbientColor, b.AmbientColor, amount),
            };
        }

        #endregion

        #region Private: discovering the environment

        private void Discover()
        {
            var roots = new List<Transform>();
            if (_root != null)
            {
                roots.Add(_root);
            }
            else
            {
                foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    roots.Add(go.transform);
                }
            }

            foreach (Transform root in roots)
            {
                Walk(root);
            }

            var random = new System.Random(_seed);
            for (Category cat = Category.Tree; cat <= Category.Fauna; cat++)
            {
                var group = new List<Item>();
                foreach (Item item in _items)
                {
                    if (item.Cat == cat)
                    {
                        group.Add(item);
                    }
                }

                int[] order = Shuffled(group.Count, random);
                for (int i = 0; i < group.Count; i++)
                {
                    Item item = group[order[i]];
                    item.Rank = i;
                    item.GroupCount = group.Count;
                    item.R = (float)random.NextDouble();
                    item.Blossom = RandomBlossom(random);
                    if (cat == Category.Tree || cat == Category.Shrub)
                    {
                        _avoid.Add(item.T.position);
                    }

                    if (cat == Category.Plant || cat == Category.Shrub)
                    {
                        _anchors.Add(item.T.position);
                    }
                }
            }

            bool hasBounds = false;
            _area = new Bounds(transform.position, Vector3.zero);
            foreach (Item item in _items)
            {
                foreach (Slot slot in item.Slots)
                {
                    if (!hasBounds)
                    {
                        _area = slot.Renderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        _area.Encapsulate(slot.Renderer.bounds);
                    }
                }
            }

            if (_useCustomArea || !hasBounds)
            {
                _area = _customArea;
            }
        }

        private void Walk(Transform t)
        {
            if (!t.gameObject.activeInHierarchy || IsIgnored(t))
            {
                return;
            }

            Category cat = Classify(t.name);
            if (cat != Category.None)
            {
                bool isGroup = t.GetComponent<Renderer>() == null && CountMatchingChildren(t, cat) >= _minGroupChildren;
                if (!isGroup)
                {
                    if (t.GetComponentInChildren<Renderer>(false) != null)
                    {
                        AddItem(t, cat);
                    }

                    return;
                }
            }

            foreach (Transform child in t)
            {
                Walk(child);
            }
        }

        private bool IsIgnored(Transform t)
        {
            if (_boardRoot != null && t.IsChildOf(_boardRoot))
            {
                return true;
            }

            if (t.GetComponent<Terrain>() != null || t.GetComponent<Camera>() != null)
            {
                return true;
            }

            Tokenize(t.name, _tokens);
            foreach (string token in _tokens)
            {
                foreach (string word in _ignoreWords)
                {
                    if (token == word)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private int CountMatchingChildren(Transform t, Category cat)
        {
            int count = 0;
            foreach (Transform child in t)
            {
                if (Classify(child.name) == cat)
                {
                    count++;
                }
            }

            return count;
        }

        private Category Classify(string objectName)
        {
            Tokenize(objectName, _tokens);
            foreach (string token in _tokens)
            {
                if (StartsWithAny(token, _treeWords))
                {
                    return Category.Tree;
                }

                if (StartsWithAny(token, _shrubWords))
                {
                    return Category.Shrub;
                }

                if (StartsWithAny(token, _plantWords))
                {
                    return Category.Plant;
                }

                if (StartsWithAny(token, _faunaWords))
                {
                    return Category.Fauna;
                }
            }

            return Category.None;
        }

        private static bool StartsWithAny(string token, string[] words)
        {
            foreach (string word in words)
            {
                if (!string.IsNullOrEmpty(word) && token.StartsWith(word.ToLowerInvariant(), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Splits "Tree_Oak (12)" or "TreeOak12" into lowercase words.</summary>
        private static void Tokenize(string objectName, List<string> tokens)
        {
            tokens.Clear();
            var word = new StringBuilder();
            for (int i = 0; i < objectName.Length; i++)
            {
                char c = objectName[i];
                if (!char.IsLetter(c))
                {
                    Flush(word, tokens);
                    continue;
                }

                if (char.IsUpper(c) && word.Length > 0 && char.IsLower(objectName[i - 1]))
                {
                    Flush(word, tokens);
                }

                word.Append(char.ToLowerInvariant(c));
            }

            Flush(word, tokens);
        }

        private static void Flush(StringBuilder word, List<string> tokens)
        {
            if (word.Length > 0)
            {
                tokens.Add(word.ToString());
                word.Length = 0;
            }
        }

        private void AddItem(Transform t, Category cat)
        {
            var item = new Item { T = t, Go = t.gameObject, Cat = cat, BaseScale = t.localScale };
            CaptureSlots(item);
            if (item.Slots.Count > 0)
            {
                _items.Add(item);
            }
        }

        private void CaptureSlots(Item item)
        {
            foreach (Renderer renderer in item.T.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null)
                    {
                        continue;
                    }

                    int colorId = material.HasProperty(BaseColorId) ? BaseColorId
                        : (material.HasProperty(ColorId) ? ColorId : -1);
                    if (colorId < 0)
                    {
                        continue;
                    }

                    Color color = material.GetColor(colorId);
                    item.Slots.Add(new Slot
                    {
                        Renderer = renderer,
                        Index = i,
                        ColorId = colorId,
                        Base = color,
                        IsTintable = IsTintable(color),
                    });
                    if (_shader == null && material.shader != null)
                    {
                        string shaderName = material.shader.name.ToLowerInvariant();
                        if (shaderName.Contains("lit") || shaderName.Contains("standard"))
                        {
                            _shader = material.shader;
                        }
                    }
                }
            }
        }

        private bool IsTintable(Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            if (saturation > 0.15f && hue > 0.12f && hue < 0.55f)
            {
                return true;
            }

            return _tintWhiteMaterials && saturation < 0.12f && value > 0.75f;
        }

        #endregion

        #region Private: new species

        private void CreateClones()
        {
            var random = new System.Random(_seed + 101);
            int total = 0;
            for (Category cat = Category.Tree; cat <= Category.Fauna; cat++)
            {
                var originals = new List<Item>();
                foreach (Item item in _items)
                {
                    if (item.Cat == cat && !item.IsClone)
                    {
                        originals.Add(item);
                    }
                }

                if (originals.Count == 0)
                {
                    continue;
                }

                int wanted = Mathf.Min(Mathf.RoundToInt(originals.Count * GetNewShare(cat)), _maxNewItems - total);
                var clones = new List<Item>();
                for (int i = 0; i < wanted; i++)
                {
                    clones.Add(MakeClone(originals[random.Next(originals.Count)], random));
                }

                for (int i = 0; i < clones.Count; i++)
                {
                    clones[i].Rank = i;
                    clones[i].GroupCount = clones.Count;
                }

                total += clones.Count;
                _items.AddRange(clones);
            }
        }

        private float GetNewShare(Category cat)
        {
            switch (cat)
            {
                case Category.Tree: return _newTreeShare;
                case Category.Shrub: return _newShrubShare;
                case Category.Plant: return _newPlantShare;
                default: return _newFaunaShare;
            }
        }

        private Item MakeClone(Item source, System.Random random)
        {
            GameObject go = Instantiate(source.Go, source.T.parent);
            go.name = source.T.name + " (new species)";
            go.SetActive(false);
            Transform t = go.transform;
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(_newItemDistance.x, _newItemDistance.y, (float)random.NextDouble());
            Vector3 position = source.T.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (source.Cat == Category.Fauna)
            {
                position.y += (float)random.NextDouble() * 1.5f;
            }
            else
            {
                position.y = GroundY(position, source.T.position.y, 1.5f);
            }

            t.position = position;
            t.rotation = Quaternion.AngleAxis((float)random.NextDouble() * 360f, Vector3.up) * source.T.rotation;
            float width = source.Cat == Category.Fauna ? Range(random, 0.8f, 1.2f) : Range(random, 0.8f, 1.3f);
            float height = source.Cat == Category.Fauna ? width : Range(random, 0.7f, 1.5f);
            Vector3 scale = Vector3.Scale(source.BaseScale, new Vector3(width, height, width));
            t.localScale = scale;
            var clone = new Item
            {
                T = t,
                Go = go,
                Cat = source.Cat,
                BaseScale = scale,
                IsClone = true,
                R = (float)random.NextDouble(),
                Blossom = RandomBlossom(random),
            };
            CaptureSlots(clone);
            return clone;
        }

        private int CountClones()
        {
            int count = 0;
            foreach (Item item in _items)
            {
                if (item.IsClone)
                {
                    count++;
                }
            }

            return count;
        }

        #endregion

        #region Private: generated infrastructure

        private void CreateTech()
        {
            _generatedRoot = new GameObject("Park Time World (generated)").transform;
            CreateMaterials();
            var random = new System.Random(_seed + 202);
            var taken = new List<Vector3>(_avoid);

            var stationSpots = new List<Vector3>();
            for (int i = 0; i < _stationCount; i++)
            {
                Vector3 spot = FindSpot(random, taken);
                taken.Add(spot);
                stationSpots.Add(spot);
                AddTech(TechKind.Station, BuildStation(spot), i, _stationCount, spot);
            }

            for (int i = 0; i < _sensorCount; i++)
            {
                Vector3 spot = FindSpot(random, taken);
                taken.Add(spot);
                AddTech(TechKind.Sensor, BuildSensor(spot), i, _sensorCount, spot);
            }

            for (int i = 0; i < _towerCount; i++)
            {
                Vector3 spot = FindSpot(random, taken);
                taken.Add(spot);
                AddTech(TechKind.Tower, BuildTower(spot), i, _towerCount, spot);
            }

            for (int i = 0; i < _zoneCount; i++)
            {
                Vector3 spot = i < stationSpots.Count ? stationSpots[i] : FindSpot(random, taken);
                AddTech(TechKind.Zone, BuildZone(spot, Range(random, 2.5f, 4.5f)), i, _zoneCount, spot);
                AddTech(TechKind.Overlay, BuildOverlay(spot), i, _zoneCount, spot);
            }

            int paths = Mathf.Max(0, stationSpots.Count - 1);
            for (int i = 0; i < paths; i++)
            {
                AddTech(TechKind.Path, BuildPath(stationSpots[i], stationSpots[i + 1]), i, paths, stationSpots[i]);
            }

            for (int i = 0; i < _droneCount; i++)
            {
                Vector3 spot = FindSpot(random, null);
                Tech drone = AddTech(TechKind.Drone, BuildDrone(out Transform[] rotors), i, _droneCount, spot);
                drone.Rotors = rotors;
                drone.Phase = (float)random.NextDouble() * Mathf.PI * 2f;
                drone.Speed = Range(random, 0.25f, 0.55f) * (random.Next(2) == 0 ? 1f : -1f);
                drone.Radius = Range(random, 0.6f, 1.2f);
            }

            for (int i = 0; i < _pollinatorCount; i++)
            {
                Vector3 anchor = _anchors.Count > 0
                    ? _anchors[random.Next(_anchors.Count)] + Vector3.up * 0.6f
                    : FindSpot(random, null) + Vector3.up * 0.8f;
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                PrepareGenerated(go, "Pollinator", _pollinatorMaterials[i % _pollinatorMaterials.Length]);
                go.transform.localScale = Vector3.one * 0.05f;
                Tech pollinator = AddTech(TechKind.Pollinator, go, i, _pollinatorCount, anchor);
                pollinator.Phase = (float)random.NextDouble() * 20f;
                pollinator.Speed = Range(random, 0.8f, 1.6f);
            }
        }

        private Tech AddTech(TechKind kind, GameObject go, int rank, int count, Vector3 spot)
        {
            var tech = new Tech
            {
                Go = go,
                T = go.transform,
                Kind = kind,
                BaseScale = go.transform.localScale,
                Rank = rank,
                GroupCount = count,
                Spot = spot,
            };
            go.SetActive(false);
            _tech.Add(tech);
            return tech;
        }

        private void CreateMaterials()
        {
            if (_shader == null)
            {
                _shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (_shader == null)
            {
                _shader = Shader.Find("Standard");
            }

            _padMaterial = MakeMaterial(new Color(0.78f, 0.80f, 0.80f), 0f);
            _darkMaterial = MakeMaterial(new Color(0.22f, 0.24f, 0.26f), 0f);
            _glowMaterial = MakeMaterial(new Color(0.8f, 0.95f, 1f), 2f);
            _zoneMaterial = MakeMaterial(new Color(0.72f, 0.74f, 0.66f), 0f);
            _pathMaterial = MakeMaterial(new Color(0.62f, 0.62f, 0.58f), 0f);
            _pollinatorMaterials = new[]
            {
                MakeMaterial(new Color(0.98f, 0.85f, 0.25f), 0.3f),
                MakeMaterial(new Color(0.98f, 0.98f, 0.95f), 0.3f),
                MakeMaterial(new Color(0.95f, 0.55f, 0.25f), 0.3f),
            };
            _ringMesh = CreateRingMesh(0.9f, 1f, 48);
        }

        private Material MakeMaterial(Color color, float emission)
        {
            var material = new Material(_shader);
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }

            if (material.HasProperty(ColorId))
            {
                material.SetColor(ColorId, color);
            }

            if (emission > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            return material;
        }

        private void PrepareGenerated(GameObject go, string objectName, Material material)
        {
            go.name = objectName;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.SetParent(_generatedRoot, false);
        }

        private GameObject Part(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            return go;
        }

        private Transform NewRoot(string objectName, Vector3 position)
        {
            Transform root = new GameObject(objectName).transform;
            root.SetParent(_generatedRoot, false);
            root.position = position;
            return root;
        }

        private GameObject BuildStation(Vector3 spot)
        {
            Transform root = NewRoot("Charging station", spot);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(1.2f, 0.04f, 1.2f), _padMaterial);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0.3f, -0.4f), new Vector3(0.3f, 0.5f, 0.3f), _darkMaterial);
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.62f, -0.4f), Vector3.one * 0.12f, _glowMaterial);
            return root.gameObject;
        }

        private GameObject BuildSensor(Vector3 spot)
        {
            Transform root = NewRoot("Sensor", spot);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(0.04f, 0.5f, 0.04f), _darkMaterial);
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 1.05f, 0f), Vector3.one * 0.15f, _padMaterial);
            return root.gameObject;
        }

        private GameObject BuildTower(Vector3 spot)
        {
            Transform root = NewRoot("Communication tower", spot);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 3.5f, 0f), new Vector3(0.1f, 3.5f, 0.1f), _padMaterial);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 6f, 0f), new Vector3(1.2f, 0.05f, 0.05f), _darkMaterial);
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 7.1f, 0f), Vector3.one * 0.3f, _glowMaterial);
            return root.gameObject;
        }

        private GameObject BuildZone(Vector3 spot, float radius)
        {
            Transform root = NewRoot("Managed zone", spot + Vector3.up * 0.03f);
            Part(root, PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius * 2f, 0.01f, radius * 2f), _zoneMaterial);
            return root.gameObject;
        }

        private GameObject BuildOverlay(Vector3 spot)
        {
            var go = new GameObject("Scan ring");
            go.transform.SetParent(_generatedRoot, false);
            go.transform.position = spot + Vector3.up * 0.08f;
            go.AddComponent<MeshFilter>().sharedMesh = _ringMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = _glowMaterial;
            go.transform.localScale = Vector3.one * 5f;
            return go;
        }

        private GameObject BuildPath(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float length = Mathf.Max(0.1f, flat.magnitude);
            Transform root = NewRoot("Path", (from + to) * 0.5f + Vector3.up * 0.04f);
            root.rotation = Quaternion.LookRotation(flat.sqrMagnitude > 0.0001f ? flat : Vector3.forward);
            Part(root, PrimitiveType.Cube, Vector3.zero, new Vector3(0.7f, 0.02f, length), _pathMaterial);
            return root.gameObject;
        }

        private GameObject BuildDrone(out Transform[] rotors)
        {
            Transform root = NewRoot("Drone", Vector3.zero);
            Part(root, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.35f, 0.12f, 0.35f), _padMaterial);
            Part(root, PrimitiveType.Sphere, new Vector3(0f, -0.08f, 0f), Vector3.one * 0.07f, _glowMaterial);
            rotors = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? 0.28f : -0.28f;
                float z = i < 2 ? 0.28f : -0.28f;
                rotors[i] = Part(root, PrimitiveType.Cylinder, new Vector3(x, 0.08f, z),
                    new Vector3(0.18f, 0.005f, 0.18f), _darkMaterial).transform;
            }

            return root.gameObject;
        }

        private static Mesh CreateRingMesh(float inner, float outer, int segments)
        {
            var vertices = new Vector3[(segments + 1) * 2];
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = direction * inner;
                vertices[i * 2 + 1] = direction * outer;
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;
            }

            var triangles = new int[segments * 12];
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                int t = i * 12;
                triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = c;
                triangles[t + 3] = c; triangles[t + 4] = b; triangles[t + 5] = d;
                triangles[t + 6] = a; triangles[t + 7] = c; triangles[t + 8] = b;
                triangles[t + 9] = c; triangles[t + 10] = d; triangles[t + 11] = b;
            }

            var mesh = new Mesh { name = "Scan ring", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private Vector3 FindSpot(System.Random random, List<Vector3> taken)
        {
            Vector3 best = default;
            float bestDistance = -1f;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float x = Mathf.Lerp(_area.min.x, _area.max.x, (float)random.NextDouble());
                float z = Mathf.Lerp(_area.min.z, _area.max.z, (float)random.NextDouble());
                var point = new Vector3(x, _area.min.y, z);
                point.y = GroundY(point, _area.min.y, float.PositiveInfinity);
                if (taken == null)
                {
                    return point;
                }

                float distance = NearestDistanceFlat(point, taken);
                if (distance >= _clearanceMeters)
                {
                    return point;
                }

                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }

            return best;
        }

        private static float NearestDistanceFlat(Vector3 point, List<Vector3> others)
        {
            float nearest = float.PositiveInfinity;
            foreach (Vector3 other in others)
            {
                float dx = other.x - point.x;
                float dz = other.z - point.z;
                nearest = Mathf.Min(nearest, Mathf.Sqrt(dx * dx + dz * dz));
            }

            return nearest;
        }

        private float GroundY(Vector3 point, float fallbackY, float maxDifference)
        {
            var origin = new Vector3(point.x, Mathf.Max(_area.max.y, fallbackY) + 20f, point.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(hit.point.y - fallbackY) <= maxDifference)
            {
                return hit.point.y;
            }

            return fallbackY;
        }

        #endregion

        #region Private: applying the strategy and the years

        private void ApplyItems(float impact, float efficiency)
        {
            float nature = impact * (1f - efficiency);
            float managed = impact * efficiency;
            foreach (Item item in _items)
            {
                float share;
                Vector3 factor = Vector3.one;
                float blossom;
                float tidy = 0f;
                if (item.IsClone)
                {
                    share = Mathf.Clamp01(nature * 1.2f);
                    blossom = 0.85f;
                }
                else
                {
                    switch (item.Cat)
                    {
                        case Category.Tree:
                            share = 1f - 0.45f * managed;
                            factor = Vector3.one * (1f + 0.3f * nature - 0.08f * managed);
                            blossom = Mathf.Clamp01((nature * 1.3f - item.R) * 4f) * 0.7f;
                            tidy = Mathf.Clamp01(managed * 1.2f) * 0.6f;
                            break;
                        case Category.Shrub:
                            share = 1f - 0.55f * managed;
                            factor = new Vector3(1f + 0.45f * nature, (1f + 0.55f * nature) * (1f - 0.4f * managed),
                                1f + 0.45f * nature);
                            blossom = Mathf.Clamp01((nature * 1.3f - item.R) * 4f) * 0.8f;
                            tidy = Mathf.Clamp01(managed * 1.2f) * 0.65f;
                            break;
                        case Category.Plant:
                            share = 1f - 0.8f * managed;
                            factor = new Vector3(1f + 0.4f * nature, (1f + 0.7f * nature) * (1f - 0.5f * managed),
                                1f + 0.4f * nature);
                            blossom = Mathf.Clamp01((nature * 1.4f - item.R) * 4f) * 0.9f;
                            tidy = Mathf.Clamp01(managed * 1.2f) * 0.65f;
                            break;
                        default:
                            share = 1f - 0.6f * managed;
                            blossom = 0f;
                            break;
                    }
                }

                float s = Reveal(share, item.Rank, item.GroupCount);
                bool isVisible = s > 0.002f;
                if (item.Go.activeSelf != isVisible)
                {
                    item.Go.SetActive(isVisible);
                }

                if (!isVisible)
                {
                    continue;
                }

                item.T.localScale = new Vector3(item.BaseScale.x * factor.x * s, item.BaseScale.y * factor.y * s,
                    item.BaseScale.z * factor.z * s);
                if (item.Cat != Category.Fauna
                    && (Mathf.Abs(blossom - item.AppliedBlossom) > 0.003f || Mathf.Abs(tidy - item.AppliedTidy) > 0.003f))
                {
                    item.AppliedBlossom = blossom;
                    item.AppliedTidy = tidy;
                    ApplyTint(item, blossom, tidy);
                }
            }
        }

        private void ApplyTint(Item item, float blossom, float tidy)
        {
            foreach (Slot slot in item.Slots)
            {
                if (!slot.IsTintable)
                {
                    continue;
                }

                Color color = slot.Base;
                if (blossom > 0f)
                {
                    color = Color.Lerp(color, item.Blossom, blossom);
                }

                if (tidy > 0f)
                {
                    color = Color.Lerp(color, _tidyColor, tidy);
                }

                slot.Renderer.GetPropertyBlock(_block, slot.Index);
                _block.SetColor(slot.ColorId, color);
                slot.Renderer.SetPropertyBlock(_block, slot.Index);
            }
        }

        private void ApplyTech(float impact, float efficiency)
        {
            float nature = impact * (1f - efficiency);
            float managed = impact * efficiency;
            foreach (Tech tech in _tech)
            {
                float s = Reveal(GetTechShare(tech.Kind, managed, nature), tech.Rank, tech.GroupCount);
                tech.S = s;
                bool isVisible = s > 0.002f;
                if (tech.Go.activeSelf != isVisible)
                {
                    tech.Go.SetActive(isVisible);
                }

                if (isVisible && tech.Kind != TechKind.Overlay && tech.Kind != TechKind.Pollinator
                    && tech.Kind != TechKind.Drone)
                {
                    tech.T.localScale = tech.BaseScale * s;
                }
            }
        }

        private static float GetTechShare(TechKind kind, float managed, float nature)
        {
            switch (kind)
            {
                case TechKind.Station: return Mathf.Clamp01((managed - 0.1f) * 1.6f);
                case TechKind.Sensor: return Mathf.Clamp01(managed * 1.4f);
                case TechKind.Tower: return Mathf.Clamp01((managed - 0.25f) * 1.8f);
                case TechKind.Drone: return Mathf.Clamp01(0.1f + managed);
                case TechKind.Overlay: return Mathf.Clamp01(managed * 1.3f);
                case TechKind.Zone:
                case TechKind.Path: return Mathf.Clamp01((managed - 0.1f) * 1.4f);
                default: return Mathf.Clamp01(0.4f + nature - 0.35f * managed);
            }
        }

        private void Tick()
        {
            float now = Time.time;
            float height = Mathf.Lerp(_droneHeight.x, _droneHeight.y, _efficiency);
            float radius = Mathf.Lerp(_droneRadius.x, _droneRadius.y, _efficiency);
            foreach (Tech tech in _tech)
            {
                if (!tech.Go.activeSelf)
                {
                    continue;
                }

                switch (tech.Kind)
                {
                    case TechKind.Drone:
                        {
                            float angle = tech.Phase + now * tech.Speed;
                            float r = radius * tech.Radius;
                            tech.T.position = tech.Spot + new Vector3(Mathf.Cos(angle) * r,
                                height + Mathf.Sin(now * 1.3f + tech.Phase) * 0.15f, Mathf.Sin(angle) * r);
                            tech.T.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                            tech.T.localScale = tech.BaseScale * tech.S;
                            foreach (Transform rotor in tech.Rotors)
                            {
                                rotor.Rotate(0f, 1500f * Time.deltaTime, 0f, Space.Self);
                            }

                            break;
                        }

                    case TechKind.Pollinator:
                        {
                            float t = now * tech.Speed + tech.Phase;
                            tech.T.position = tech.Spot + new Vector3(Mathf.Sin(t * 1.1f) * 0.8f,
                                0.3f + Mathf.Sin(t * 1.7f) * 0.2f, Mathf.Cos(t * 0.9f) * 0.8f);
                            tech.T.localScale = tech.BaseScale * tech.S;
                            break;
                        }

                    case TechKind.Overlay:
                        {
                            float pulse = 0.9f + 0.15f * Mathf.Sin(now * 2f + tech.Rank);
                            tech.T.localScale = tech.BaseScale * (tech.S * pulse);
                            break;
                        }
                }
            }
        }

        private static float Reveal(float share, int rank, int count)
        {
            float x = Mathf.Clamp01(Mathf.Clamp01(share) * count - rank);
            return x * x * (3f - 2f * x);
        }

        #endregion

        #region Private: small helpers

        private Color RandomBlossom(System.Random random)
        {
            return _blossomColors != null && _blossomColors.Length > 0
                ? _blossomColors[random.Next(_blossomColors.Length)]
                : Color.white;
        }

        private static float Range(System.Random random, float min, float max)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        private static int[] Shuffled(int count, System.Random random)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
        }

        private static T FindFirst<T>() where T : UnityEngine.Object
        {
#if UNITY_2022_2_OR_NEWER
            return FindFirstObjectByType<T>();
#else
			return FindObjectOfType<T>();
#endif
        }

        private static T[] FindAll<T>() where T : UnityEngine.Object
        {
#if UNITY_2022_2_OR_NEWER
            return FindObjectsByType<T>(FindObjectsSortMode.None);
#else
			return FindObjectsOfType<T>();
#endif
        }

        #endregion
    }
}