using UnityEngine;

public class QuestDebugInput : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9))
        {
            QuestManager.I.TryStartQuestById("JEANNE");
            Debug.Log("퀘스트 시작");
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            QuestManager.I.PushEvent(QuestEventType.Custom, "S1");
        }
    }
}
