using Hollow.Presentation;
using Hollow.UI;
using UnityEditor;
using UnityEngine;

public static class HollowContentPanels
{
    [MenuItem("Hollow/Audio/Music Mix Levels")]
    static void MusicMix()
    {
        var asset = Resources.Load<MusicMixSettings>("Audio/MusicMixSettings");
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }

    [MenuItem("Hollow/Dialogue/Operator Bubble Lines")]
    static void OperatorLines()
    {
        var asset = Resources.Load<OperatorBubbleLines>("Narrative/OperatorBubbleLines");
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
    }
}
