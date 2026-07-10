using UnityEngine;

public class PlayerResources : MonoBehaviour
{
    public static PlayerResources Instance { get; private set; }

    public int minerals = 0;

    private GUIStyle labelStyle;
    private GUIStyle boxStyle;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void AddMinerals(int amount)
    {
        minerals += amount;
    }

    private void OnGUI()
    {
        EnsureGUIStyle();

        Rect boxRect = new Rect(20, 50, 220, 42);
        GUI.Box(boxRect, "", boxStyle);
        GUI.Label(new Rect(32, 58, 200, 30), "Minerals: " + minerals, labelStyle);
    }

    private void EnsureGUIStyle()
    {
        if (labelStyle != null)
        {
            return;
        }

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 22;
        labelStyle.normal.textColor = Color.white;
        labelStyle.fontStyle = FontStyle.Bold;

        boxStyle = new GUIStyle(GUI.skin.box);
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
        texture.Apply();
        boxStyle.normal.background = texture;
    }
}
