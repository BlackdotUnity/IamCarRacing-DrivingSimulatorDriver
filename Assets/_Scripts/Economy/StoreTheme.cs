using UnityEngine;

/// <summary>Fonts / sprites the store popup and cost buttons borrow from the game UI. Lives in Resources/Economy.</summary>
[CreateAssetMenu(menuName = "Economy/Store Theme", fileName = "StoreTheme")]
public class StoreTheme : ScriptableObject
{
    public const string ResourcePath = "Economy/StoreTheme";

    public Font font;
    public Sprite panel;
    public Sprite header;
    public Sprite closeButton;
    public Sprite packButton;
    public Sprite coinIcon;
    public Sprite keyIcon;
    public Color headerTextColor = new Color(0.06f, 0.06f, 0.06f);
    public Color buttonTextColor = new Color(0.06f, 0.06f, 0.06f);
}
