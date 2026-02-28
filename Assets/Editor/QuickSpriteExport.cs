using UnityEngine;
using UnityEditor;
using System.IO;

public class QuickSpriteExport
{
    [MenuItem("Tools/Quick Export Selected Sprite")]
    static void Export()
    {
        Sprite sprite = Selection.activeObject as Sprite;
        if (sprite == null) return;

        Texture2D tex = sprite.texture;
        Rect r = sprite.rect;

        Texture2D newTex = new Texture2D((int)r.width, (int)r.height);
        newTex.SetPixels(tex.GetPixels(
            (int)r.x,
            (int)r.y,
            (int)r.width,
            (int)r.height));
        newTex.Apply();

        byte[] bytes = newTex.EncodeToPNG();
        File.WriteAllBytes(
            Application.dataPath + "/" + sprite.name + ".png",
            bytes);

        AssetDatabase.Refresh();
    }
}
