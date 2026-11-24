using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class KenneyTheme
{
    public static readonly Color Ink = new Color(.045f, .06f, .095f, .98f);
    public static readonly Color Card = new Color(.075f, .10f, .15f, .98f);
    public static readonly Color Line = new Color(.27f, .32f, .41f);
    public static readonly Color Gold = new Color(.91f, .75f, .46f);
    public static readonly Color Rose = new Color(.98f, .36f, .51f);
    public static readonly Color Paper = new Color(.94f, .92f, .86f);
    public static readonly Color Muted = new Color(.56f, .64f, .73f);
    public static readonly Color Mint = new Color(.41f, .84f, .71f);
    private static Font font;
    private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
    public static Sprite Sprite(string path)
    {
        Sprite sprite;
        if (!sprites.TryGetValue(path, out sprite) || sprite == null)
        {
            sprite = Resources.Load<Sprite>("KenneyUI/" + path);
            sprites[path] = sprite;
        }
        return sprite;
    }
    public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h, Vector2? anchor = null)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    public static Image Image(Transform parent, string name, float x, float y, float w, float h, Color color, string sprite = null, Vector2? anchor = null)
    {
        RectTransform rect = Rect(parent, name, x, y, w, h, anchor);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (!string.IsNullOrEmpty(sprite))
        {
            image.sprite = Sprite(sprite);
            image.type = image.sprite != null && image.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        }
        return image;
    }
    public static RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, Color? border = null, Vector2? anchor = null)
    {
        Image fill = Image(parent, name, x, y, w, h, Ink, "Frames/Panel", anchor);
        Image outline = Image(fill.transform, "Marco_Kenney", 0, 0, w, h, border ?? Line, "Frames/Frame");
        Stretch(outline.rectTransform);
        return fill.rectTransform;
    }
    public static Text Label(Transform parent, string name, string value, float x, float y, float w, float h, int size = 20, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft, Vector2? anchor = null)
    {
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Text text = Rect(parent, name, x, y, w, h, anchor).gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = color ?? Paper;
        text.text = value;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        text.supportRichText = false;
        return text;
    }
    public static Button Button(Transform parent, string label, string icon, float x, float y, float w, float h, Action action, bool primary = false, bool danger = false)
    {
        Color accent = danger ? Rose : primary ? Gold : Line;
        Image fill = Image(parent, "Boton_" + label, x, y, w, h, primary ? new Color(.23f, .19f, .13f) : Card, "Frames/Button");
        fill.raycastTarget = true;
        Image frame = Image(fill.transform, "Marco", 0, 0, w, h, accent, "Frames/ButtonFrame");
        Stretch(frame.rectTransform);
        if (!string.IsNullOrEmpty(icon))
            Image(fill.transform, "Icono", -w * .5f + 30, 0, 24, 24, primary ? Gold : Muted, "Icons/" + icon).preserveAspect = true;
        Text text = Label(fill.transform, "Texto", label, icon == null ? 0 : 17, 0, w - (icon == null ? 24 : 88), h - 10, 21, Paper, icon == null ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 16;
        text.resizeTextMaxSize = 21;
        Button button = fill.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.55f, 1.55f, 1.55f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(.75f, .75f, .75f);
        colors.disabledColor = new Color(.35f, .35f, .35f, .5f);
        colors.fadeDuration = .08f;
        button.colors = colors;
        fill.gameObject.AddComponent<KenneyButtonFeedback>().Initialize(frame, accent);
        button.onClick.AddListener(() => { KenneyUISound.Play("Click"); action?.Invoke(); });
        return button;
    }
    public static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }
}
