using UnityEngine;
using DG.Tweening;

public class UIPopupAnimation : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("How long the popup takes to appear")]
    public float Duration = 0.5f;

    [Tooltip("The easing function for the 'pop' effect. OutBack is standard for popups.")]
    public Ease AnimationEase = Ease.OutBack;

    [Tooltip("Delay before animation starts")]
    public float StartDelay = 0f;

    [Header("Optional")]
    [Tooltip("If true, also fades in a CanvasGroup if attached")]
    public bool FadeIn = true;

    private CanvasGroup _canvasGroup;
    private Vector3 _originalScale;

    private void Awake()
    {
        _originalScale = transform.localScale;
        // If we want to support non-uniform scales, we save it here. 
        // For most UI panels, it's Vector3.one.
        if (_originalScale == Vector3.zero) _originalScale = Vector3.one;

        _canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        // 1. Reset State
        transform.localScale = Vector3.zero;
        if (_canvasGroup != null && FadeIn)
        {
            _canvasGroup.alpha = 0f;
        }

        // 2. Animate
        Sequence seq = DOTween.Sequence();
        seq.SetDelay(StartDelay);

        // Scale Up
        seq.Append(transform.DOScale(_originalScale, Duration).SetEase(AnimationEase));

        // Fade In (Concurrent)
        if (_canvasGroup != null && FadeIn)
        {
            seq.Join(_canvasGroup.DOFade(1f, Duration * 0.8f));
        }

        seq.Play();
    }
}
