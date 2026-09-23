using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Renderer))]
public class Fade : MonoBehaviour
{
    [SerializeField] private float duration = 1f;
    [SerializeField] private bool fadeOutOnStart = true;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Renderer rend;
    private MaterialPropertyBlock block;
    private Color baseColor;
    private Coroutine current;

    public bool IsFading => current != null;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        baseColor = rend.sharedMaterial.GetColor(BaseColorId);
    }

    private void Start()
    {
        if (fadeOutOnStart)
        {
            SetAlpha(1f);
            FadeOut();
        }
        else
        {
            SetAlpha(0f);
        }
    }

    // Testing: Q = fade in, E = fade out
    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.zKey.wasPressedThisFrame)
            FadeIn();
        else if (Keyboard.current.xKey.wasPressedThisFrame)
            FadeOut();
    }

    // Opacity 0 -> 1 (plane covers the view)
    public void FadeIn() => StartFade(1f);

    // Opacity 1 -> 0 (plane disappears)
    public void FadeOut() => StartFade(0f);

    public Coroutine StartFade(float target)
    {
        if (current != null)
            StopCoroutine(current);
        current = StartCoroutine(FadeRoutine(target));
        return current;
    }

    private IEnumerator FadeRoutine(float target)
    {
        float start = GetAlpha();
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(start, target, time / duration));
            yield return null;
        }

        SetAlpha(target);
        current = null;
    }

    private float GetAlpha()
    {
        rend.GetPropertyBlock(block);
        return block.isEmpty ? baseColor.a : block.GetColor(BaseColorId).a;
    }

    private void SetAlpha(float alpha)
    {
        Color c = baseColor;
        c.a = alpha;
        block.SetColor(BaseColorId, c);
        rend.SetPropertyBlock(block);
        rend.enabled = alpha > 0f;
    }
}
