using FlickerVoid.Engine.Gameplay.Conditions;
using FlickerVoid.Engine.Gameplay.Facts;
using FlickerVoid.Engine.Rendering.Serialization;
using FlickerVoid.Engine.Runtime.Content;

namespace FlickerVoid.Engine.Rendering.Lighting;

/// <summary>
/// Picks the lighting mood the story wants whenever the facts change: the mood of the hardest rule that holds, or the
/// default when none does. It blends from the mood on screen to the new one over <see cref="BlendSeconds"/>.
/// </summary>
public sealed class LightingMoodDirector : IDisposable
{
    private readonly IFactDatabase _facts;
    private readonly IContentManager _content;
    private readonly string _defaultMood;
    private readonly IReadOnlyList<MoodRule> _rules;
    private readonly ConditionSet[] _conditions;
    private LightingMood _from;
    private LightingMood _to;
    private float _elapsed;
    private bool _blended = true;

    public LightingMoodDirector(IFactDatabase facts, IContentManager content, string defaultMood, IReadOnlyList<MoodRule> rules)
    {
        _facts = facts;
        _content = content;
        _defaultMood = defaultMood;
        _rules = rules;
        _conditions = rules.Select(rule => rule.When).ToArray();

        Target = ChooseMood();
        _to = Load(Target);
        _from = _to;
        Current = _to;
        _facts.FactsChanged += HandleFactsChanged;
    }

    public float BlendSeconds { get; set; } = 2;

    /// <summary>The path of the mood the director is blending toward.</summary>
    public string Target { get; private set; }

    /// <summary>The mood to render now.</summary>
    public LightingMood Current { get; private set; }

    /// <summary>True from a change of mood until the blend to it is done; while false, <see cref="Current"/> stays the same object.</summary>
    public bool IsBlending => !_blended;

    public void Update(float deltaSeconds)
    {
        if (_blended)
        {
            return;
        }

        _elapsed += deltaSeconds;
        float amount = 1;
        if (BlendSeconds > 0)
        {
            amount = Math.Clamp(_elapsed / BlendSeconds, 0, 1);
        }

        Current = LightingMood.Lerp(_from, _to, amount);
        _blended = amount >= 1;
    }

    public void Dispose()
    {
        _facts.FactsChanged -= HandleFactsChanged;
    }

    private void HandleFactsChanged()
    {
        string mood = ChooseMood();
        if (mood == Target)
        {
            return;
        }

        Target = mood;
        _from = Current;
        _to = Load(mood);
        _elapsed = 0;
        _blended = false;
    }

    private string ChooseMood()
    {
        ConditionContext context = new(_facts);
        int[] held = ConditionSet.Rank(_conditions, context);
        if (held.Length == 0)
        {
            return _defaultMood;
        }

        return _rules[held[0]].Mood;
    }

    private LightingMood Load(string path)
    {
        return _content.Load(path, RenderingJsonContext.Default.LightingMood);
    }
}

