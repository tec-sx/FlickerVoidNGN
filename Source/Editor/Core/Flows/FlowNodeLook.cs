namespace FlickerVoid.Editor.Core.Flows;

/// <summary>What kind of node a type is, which tints its header, and the icon beside its title (named as in the shell's icon set).</summary>
public sealed record FlowNodeLook(string Kind, string Icon)
{
    private static readonly Dictionary<string, FlowNodeLook> Known = new()
    {
        ["start"] = new("Start", "Play"),
        ["finish"] = new("Finish", "Stop"),
        ["line"] = new("Dialogue", "Talk"),
        ["choice"] = new("Choice", "Branch"),
        ["completeObjective"] = new("Quest", "Flag"),
        ["setQuestState"] = new("Quest", "Flag"),
        ["learn"] = new("Quest", "Flag"),
        ["playShot"] = new("Camera", "Camera"),
        ["switchShot"] = new("Camera", "Camera"),
        ["shakeCamera"] = new("Camera", "Camera"),
        ["fade"] = new("Camera", "Camera"),
        ["branch"] = new("Condition", "Branch"),
        ["fact"] = new("Condition", "Branch"),
        ["hasItem"] = new("Condition", "Branch"),
        ["knows"] = new("Condition", "Branch"),
        ["questState"] = new("Condition", "Branch"),
        ["waitForConditions"] = new("Condition", "Clock"),
    };

    private static readonly FlowNodeLook Other = new("Effect", "Effects");

    /// <summary>The look of a node type; types not listed look like effects.</summary>
    public static FlowNodeLook For(string type)
    {
        if (Known.TryGetValue(type, out FlowNodeLook? look))
        {
            return look;
        }

        return Other;
    }
}
