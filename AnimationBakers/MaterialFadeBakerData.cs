using System;
using System.Collections.Generic;

[Serializable]
public class MaterialFadeEntry
{
    public string materialPath;
    public bool   hasFadeIn;
    public int    fadeInStart;
    public int    fadeInEnd;
    public int    fadeOutStart;
    public int    fadeOutEnd;
    public int    renderQueue = 3000;
}

[Serializable]
public class FadeLogEntry
{
    public string       timestamp;
    public string       clipName;
    public List<string> lines = new List<string>();
}

[Serializable]
public class FadeBakerJson
{
    public string                  clipGUID;
    public List<MaterialFadeEntry> entries = new List<MaterialFadeEntry>();
    public List<FadeLogEntry>      log     = new List<FadeLogEntry>();
}
