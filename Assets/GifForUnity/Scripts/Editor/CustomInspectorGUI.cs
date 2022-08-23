using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GifAssetImporter))]
public class CustomInspectorGUI : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        GUI.TextField(new Rect(Vector2.zero, new Vector2(100, 20)), "Hello");
    }
}
