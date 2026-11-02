using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class KenneyButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private Image frame;
    private Color normal;
    private bool hovered, selected;
    public void Initialize(Image image,Color color) { frame=image; normal=color; }
    private bool CanInteract { get { Selectable item=GetComponent<Selectable>(); return item != null && item.IsInteractable(); } }
    public void OnPointerEnter(PointerEventData data) { hovered=true; if (CanInteract) KenneyUISound.Play("Hover"); }
    public void OnPointerExit(PointerEventData data) { hovered=false; }
    public void OnSelect(BaseEventData data) { selected=true; }
    public void OnDeselect(BaseEventData data) { selected=false; }
    private void Update()
    {
        if (frame == null) return;
        Color target=!CanInteract ? normal*.35f : hovered || selected ? KenneyTheme.Gold : normal;
        frame.color=Color.Lerp(frame.color,target,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
    }
}
