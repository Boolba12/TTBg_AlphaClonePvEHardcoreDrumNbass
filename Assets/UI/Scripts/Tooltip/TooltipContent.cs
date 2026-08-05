using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class TooltipValueLine
{
    public string label;
    public string value;
}

[Serializable]
public sealed class TooltipContent
{
    public string title;
    [TextArea] public string body;
    public Sprite icon;
    public List<TooltipValueLine> values = new List<TooltipValueLine>();
}

public interface ITooltipSource
{
    TooltipContent Tooltip { get; }
}
