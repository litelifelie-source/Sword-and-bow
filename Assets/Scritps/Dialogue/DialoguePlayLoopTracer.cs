using System;
using System.Reflection;
using UnityEngine;

public class DialoguePlayLoopTracer : MonoBehaviour
{
    public DialogueManager dialogueManager;
    public DialogueUI dialogueUI;

    public bool enable = true;
    public bool scanCallbacks = true;

    private string _prevSig = "";

    void Update()
    {
        if (!enable) return;
        if (dialogueManager == null)
            dialogueManager = FindFirstObjectByType<DialogueManager>();

        if (!scanCallbacks || dialogueManager == null) return;

        var t = dialogueManager.GetType();
        var f = t.GetField("_onCompleteBool", BindingFlags.Instance | BindingFlags.NonPublic);

        if (f == null) return;

        var del = f.GetValue(dialogueManager) as Delegate;

        string sig = BuildSignature(del);

        if (sig != _prevSig)
        {
            _prevSig = sig;

            if (del == null)
            {
                Debug.Log("[DLT] _onCompleteBool = null", this);
            }
            else
            {
                Debug.Log(DumpDelegate(del), this);
            }
        }
    }

    static string BuildSignature(Delegate del)
    {
        if (del == null) return "null";

        var m = del.Method;
        string typeName = m.DeclaringType != null ? m.DeclaringType.FullName : "(no type)";
        string targetName = del.Target != null ? del.Target.GetType().FullName : "(static)";
        int count = del.GetInvocationList().Length;

        return $"{targetName}->{typeName}.{m.Name}[{count}]";
    }

    static string DumpDelegate(Delegate del)
    {
        var sb = new System.Text.StringBuilder();
        var list = del.GetInvocationList();

        sb.AppendLine($"[DLT] _onCompleteBool invocations={list.Length}");

        for (int i = 0; i < list.Length; i++)
        {
            var d = list[i];
            var m = d.Method;
            string typeName = m.DeclaringType != null ? m.DeclaringType.FullName : "(no type)";
            string targetName = d.Target != null ? d.Target.GetType().FullName : "(static)";

            sb.AppendLine($"  #{i}: {targetName}->{typeName}.{m.Name}");
        }

        return sb.ToString();
    }
}