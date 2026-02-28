using UnityEngine;

public class DialogueSource : MonoBehaviour
{
    public string speaker = "NPC";

    [TextArea(2, 6)]
    public string[] lines;

    public void Play(DialogueUI ui)
    {
        if (ui == null) return;
        if (lines == null || lines.Length == 0) return;

        ui.Open(speaker, lines);
    }
}