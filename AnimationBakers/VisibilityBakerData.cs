using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VisibilityBakerData", menuName = "Tools/Visibility Baker Data")]
public class VisibilityBakerData : ScriptableObject
{
    public List<VisibilityObjectEntry> entries = new List<VisibilityObjectEntry>();
    public int frameRate = 30;
    // NOTE: no AnimationClip field here — lives in EditorPrefs so clip deletion never corrupts this
}

[System.Serializable]
public class VisibilityObjectEntry
{
    public GameObject targetObject;
    public string     objectName;  // fallback display if ref is lost
    public string     scenePath;   // full hierarchy path for re-resolution after domain reload
    public List<VisibilityRange> ranges = new List<VisibilityRange>();
}

[System.Serializable]
public class VisibilityRange
{
    public int frameOn;
    public int frameOff;
}
