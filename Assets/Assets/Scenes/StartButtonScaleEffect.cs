using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class StartButtonScaleEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] float hoverScale = 1.08f;
    [SerializeField] float scaleSpeed = 12f;

    RectTransform rectTransform;
    Vector3 normalScale;
    Vector3 targetScale;

    void Awake()
    {
        rectTransform = (RectTransform)transform;
        normalScale = Vector3.one;
        targetScale = normalScale;
        rectTransform.localScale = normalScale;
    }

    void Update()
    {
        rectTransform.localScale = Vector3.Lerp(
            rectTransform.localScale,
            targetScale,
            scaleSpeed * Time.unscaledDeltaTime);
    }

    public void OnPointerEnter(PointerEventData eventData) => SetHighlighted(true);

    public void OnPointerExit(PointerEventData eventData) => SetHighlighted(false);

    public void OnSelect(BaseEventData eventData) => SetHighlighted(true);

    public void OnDeselect(BaseEventData eventData) => SetHighlighted(false);

    void SetHighlighted(bool highlighted)
    {
        targetScale = highlighted ? normalScale * hoverScale : normalScale;
    }
}
