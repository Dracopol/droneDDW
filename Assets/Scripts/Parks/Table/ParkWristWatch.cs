using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace DroneDDW.Parks.Table
{

    [DisallowMultipleComponent]
    [AddComponentMenu("DroneDDW/Parks/Park Wrist Watch")]
    public sealed class ParkWristWatch : MonoBehaviour
    {
        #region Serialized fields

        [Header("References (all found automatically when empty)")]
        [SerializeField] private ParkTimeController _timeController;
        [SerializeField] private ParkTimeWorld _world;

        [SerializeField, Tooltip("The XR Origin's camera offset, which hand joints are relative to. Empty = the parent "
            + "of the main camera.")]
        private Transform _trackingSpace;

        [SerializeField, Tooltip("The XR camera. Falls back to Camera.main when left empty.")]
        private Transform _head;

        [Header("Placement")]
        [SerializeField, Tooltip("Tick if the watch ends up on the palm side of the wrist.")]
        private bool _flipSide;

        [SerializeField, Tooltip("How far from the wrist joint towards the elbow the watch sits.")]
        private float _towardElbowMeters = 0.04f;

        [SerializeField, Tooltip("Moves the whole watch away from (positive) or into the wrist, along the back of the hand.")]
        private float _upMeters;

        [SerializeField, Min(0f), Tooltip("Smooths the watch against tracking jitter. 0 = off.")]
        private float _smoothing = 30f;

        [SerializeField, Tooltip("Strap radius in metres. Change before pressing Play.")]
        private float _bandRadiusMeters = 0.036f;

        [Header("Screen")]
        [SerializeField, Range(-1f, 1f), Tooltip("How directly the screen must face the eyes to light up. Lower = lights "
            + "up from a wider angle.")]
        private float _facingThreshold = 0.35f;

        [SerializeField, Min(0.1f), Tooltip("How quickly the screen fades in and out.")]
        private float _fadeSpeed = 8f;

        [SerializeField] private Color _screenColor = new Color(0.8f, 0.95f, 1f);
        [SerializeField] private Color _previewColor = new Color(1f, 0.75f, 0.3f);

        [Header("Testing")]
        [SerializeField, Tooltip("Shows the watch in front of the camera when no hand is tracked, to check the look.")]
        private bool _previewWithoutHand;

        #endregion

        #region Fields

        private const float ScreenTopY = 0.0504f;

        private static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private static readonly Quaternion TextRotation = Quaternion.LookRotation(Vector3.down, Vector3.left);

        private XRHandSubsystem _subsystem;
        private Transform _root;
        private TextMeshPro _topText;
        private TextMeshPro _yearText;
        private TextMeshPro _bottomText;
        private Renderer[] _pips = new Renderer[ParkTimes.Count];
        private Shader _shader;
        private Material _rimMaterial;
        private Material _pipOnMaterial;
        private Material _pipOffMaterial;
        private bool _isBuilt;
        private bool _hasPose;
        private bool _isVisible;
        private float _facing;
        private float _screenLevel;
        private string _shownKey;
        private int _shownPip = -1;

        #endregion

        #region Setup

        private void Start()
        {
            if (_timeController == null)
            {
                _timeController = FindFirst<ParkTimeController>();
            }

            if (_world == null)
            {
                _world = FindFirst<ParkTimeWorld>();
            }

            if (_timeController == null)
            {
                Debug.LogWarning("[Parks] Park Wrist Watch needs a Park Time Controller in the scene.", this);
                enabled = false;
                return;
            }

            Build();
            _root.gameObject.SetActive(false);
            _isBuilt = true;
        }

        private void OnEnable()
        {
            Application.onBeforeRender += Place;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= Place;
        }

        private void OnDestroy()
        {
            if (_root != null)
            {
                Destroy(_root.gameObject);
            }
        }

        #endregion

        #region Update

        private void LateUpdate()
        {
            if (!_isBuilt || !_isVisible)
            {
                return;
            }

            UpdateScreen();
        }

        #endregion

        #region Private: placing

        private void Place()
        {
            if (!_isBuilt)
            {
                return;
            }

            Transform head = ResolveHead();
            bool isPlaced = TryGetWristFrame(head, out Vector3 position, out Vector3 forward, out Vector3 normal);
            if (!isPlaced && _previewWithoutHand && head != null)
            {
                position = head.position + head.rotation * new Vector3(-0.2f, -0.25f, 0.45f);
                normal = (head.position - position).normalized;
                forward = Vector3.ProjectOnPlane(head.right, normal).normalized;
                isPlaced = true;
            }

            if (isPlaced != _isVisible)
            {
                _isVisible = isPlaced;
                _root.gameObject.SetActive(isPlaced);
                _hasPose = false;
            }

            if (!isPlaced)
            {
                return;
            }

            Vector3 target = position - forward * _towardElbowMeters + normal * _upMeters;
            Quaternion targetRotation = Quaternion.LookRotation(forward, normal);
            if (!_hasPose || _smoothing <= 0f)
            {
                _root.SetPositionAndRotation(target, targetRotation);
                _hasPose = true;
            }
            else
            {
                float follow = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);
                _root.SetPositionAndRotation(Vector3.Lerp(_root.position, target, follow),
                    Quaternion.Slerp(_root.rotation, targetRotation, follow));
            }

            _facing = head != null
                ? Vector3.Dot(_root.up, (head.position - _root.position).normalized)
                : 1f;
        }

        private bool TryGetWristFrame(Transform head, out Vector3 position, out Vector3 forward, out Vector3 normal)
        {
            position = default;
            forward = default;
            normal = default;
            if (!TryGetSubsystem(out XRHandSubsystem subsystem))
            {
                return false;
            }

            XRHand hand = subsystem.leftHand;
            if (!hand.isTracked || !hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose pose))
            {
                return false;
            }

            Transform space = ResolveTrackingSpace();
            position = space != null ? space.TransformPoint(pose.position) : pose.position;
            Quaternion rotation = space != null ? space.rotation * pose.rotation : pose.rotation;
            forward = rotation * Vector3.forward;
            normal = rotation * Vector3.up * (_flipSide ? -1f : 1f);
            normal = Vector3.ProjectOnPlane(normal, forward).normalized;
            return normal.sqrMagnitude > 0.5f;
        }

        private bool TryGetSubsystem(out XRHandSubsystem subsystem)
        {
            if (_subsystem == null || !_subsystem.running)
            {
                _subsystem = null;
                SubsystemManager.GetSubsystems(Subsystems);
                foreach (XRHandSubsystem candidate in Subsystems)
                {
                    if (candidate.running)
                    {
                        _subsystem = candidate;
                        break;
                    }
                }
            }

            subsystem = _subsystem;
            return subsystem != null;
        }

        private Transform ResolveTrackingSpace()
        {
            if (_trackingSpace != null)
            {
                return _trackingSpace;
            }

            Transform head = ResolveHead();
            return head != null ? head.parent : null;
        }

        private Transform ResolveHead()
        {
            if (_head != null)
            {
                return _head;
            }

            Camera main = Camera.main;
            return main != null ? main.transform : null;
        }

        #endregion

        #region Private: screen

        private void UpdateScreen()
        {
            float target = _facing >= _facingThreshold ? 1f : 0f;
            _screenLevel = Mathf.MoveTowards(_screenLevel, target, _fadeSpeed * Time.deltaTime);

            ParkLevelSlider slider = _timeController.Slider;
            bool isPreview = slider != null && slider.IsDragging;
            int step = isPreview ? ParkTimes.Clamp(slider.Detent) : ParkTimes.Clamp(Mathf.RoundToInt(_timeController.Progress));
            int year = isPreview ? ParkTimes.GetYear(step) : Mathf.RoundToInt(GetYear(_timeController.Progress));
            int side = GetSide();

            int alpha = Mathf.RoundToInt(_screenLevel * 20f);
            string key = $"{year}|{side}|{isPreview}|{alpha}";
            if (key != _shownKey)
            {
                _shownKey = key;
                Color color = (isPreview ? _previewColor : _screenColor) * 1f;
                color.a = _screenLevel;
                _topText.text = side == 0 ? "NATURE" : (side == 2 ? "EFFICIENCY" : "BALANCED");
                _yearText.text = year.ToString();
                int offset = year - ParkTimes.GetYear(0);
                _bottomText.text = offset <= 0 ? "NOW" : $"+{offset} YRS";
                _topText.color = new Color(_screenColor.r, _screenColor.g, _screenColor.b, _screenLevel * 0.7f);
                _yearText.color = color;
                _bottomText.color = new Color(_screenColor.r, _screenColor.g, _screenColor.b, _screenLevel * 0.7f);
                SetEmission(_rimMaterial, _screenColor, 0.15f + 1.6f * _screenLevel);
            }

            if (step != _shownPip || alpha == 0)
            {
                _shownPip = step;
                for (int i = 0; i < _pips.Length; i++)
                {
                    _pips[i].sharedMaterial = i == step && _screenLevel > 0.05f ? _pipOnMaterial : _pipOffMaterial;
                }
            }
        }

        private int GetSide()
        {
            float efficiency = _world != null ? _world.Efficiency : 0.5f;
            return efficiency < 0.4f ? 0 : (efficiency > 0.6f ? 2 : 1);
        }

        private static float GetYear(float progress)
        {
            float t = Mathf.Clamp(progress, 0f, 2f);
            return t <= 1f
                ? Mathf.Lerp(ParkTimes.GetYear(0), ParkTimes.GetYear(1), t)
                : Mathf.Lerp(ParkTimes.GetYear(1), ParkTimes.GetYear(2), t - 1f);
        }

        #endregion

        #region Private: building the watch

        private void Build()
        {
            CreateMaterials(out Material band, out Material caseMaterial, out Material screen);
            _root = new GameObject("Wrist Watch").transform;

            const int segments = 16;
            float width = 2f * Mathf.PI * _bandRadiusMeters / segments * 1.15f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                GameObject block = Part(PrimitiveType.Cube, direction * _bandRadiusMeters,
                    new Vector3(width, 0.004f, 0.022f), band);
                block.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
            }

            float caseCenterY = _bandRadiusMeters + 0.006f;
            Part(PrimitiveType.Cylinder, new Vector3(0f, caseCenterY, 0f), new Vector3(0.058f, 0.006f, 0.058f),
                caseMaterial);
            GameObject crown = Part(PrimitiveType.Cylinder, new Vector3(0f, caseCenterY, 0.032f),
                new Vector3(0.008f, 0.0035f, 0.008f), caseMaterial);
            crown.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cylinder, new Vector3(0f, caseCenterY + 0.0068f, 0f),
                new Vector3(0.054f, 0.0008f, 0.054f), _rimMaterial);
            Part(PrimitiveType.Cylinder, new Vector3(0f, caseCenterY + 0.0078f, 0f),
                new Vector3(0.048f, 0.0006f, 0.048f), screen);

            float textY = ScreenTopY + 0.0003f;
            _topText = MakeText("Strategy", new Vector3(-0.0155f, textY, 0f), 0.05f, FontStyles.Bold);
            _yearText = MakeText("Year", new Vector3(-0.0015f, textY, 0f), 0.16f, FontStyles.Bold);
            _bottomText = MakeText("Offset", new Vector3(0.0105f, textY, 0f), 0.05f, FontStyles.Normal);
            for (int i = 0; i < _pips.Length; i++)
            {
                float z = (i - (_pips.Length - 1) * 0.5f) * 0.008f;
                _pips[i] = Part(PrimitiveType.Cylinder, new Vector3(0.0195f, ScreenTopY + 0.0002f, z),
                    new Vector3(0.004f, 0.0003f, 0.004f), _pipOffMaterial).GetComponent<Renderer>();
            }
        }

        private TextMeshPro MakeText(string objectName, Vector3 localPosition, float size, FontStyles style)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = TextRotation;
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(0.06f, 0.02f);
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = new Color(1f, 1f, 1f, 0f);
            return text;
        }

        private GameObject Part(PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            go.GetComponent<Renderer>().sharedMaterial = material;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            return go;
        }

        private void CreateMaterials(out Material band, out Material caseMaterial, out Material screen)
        {
            _shader = Shader.Find("Universal Render Pipeline/Lit");
            if (_shader == null)
            {
                _shader = Shader.Find("Standard");
            }

            if (_shader == null)
            {
                foreach (MeshRenderer renderer in FindAll<MeshRenderer>())
                {
                    if (renderer.sharedMaterial != null)
                    {
                        _shader = renderer.sharedMaterial.shader;
                        break;
                    }
                }
            }

            band = MakeMaterial(new Color(0.12f, 0.13f, 0.14f));
            caseMaterial = MakeMaterial(new Color(0.55f, 0.58f, 0.60f));
            screen = MakeMaterial(new Color(0.015f, 0.02f, 0.025f));
            _rimMaterial = MakeMaterial(_screenColor);
            SetEmission(_rimMaterial, _screenColor, 0.15f);
            _pipOffMaterial = MakeMaterial(new Color(0.15f, 0.17f, 0.18f));
            _pipOnMaterial = MakeMaterial(_screenColor);
            SetEmission(_pipOnMaterial, _screenColor, 2f);
        }

        private Material MakeMaterial(Color color)
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

            return material;
        }

        private static void SetEmission(Material material, Color color, float intensity)
        {
            if (!material.HasProperty(EmissionColorId))
            {
                return;
            }

            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            material.SetColor(EmissionColorId, color * intensity);
        }

        #endregion

        #region Private: helpers

        private static T FindFirst<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return FindFirstObjectByType<T>();
#else
			return FindObjectOfType<T>();
#endif
        }

        private static T[] FindAll<T>() where T : Object
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