namespace FlickerVoid.Engine.Attributes.Checks;

/// <summary>What a check came to: whether the total met the difficulty, and the lines it adds up.</summary>
public sealed record CheckResult(bool Success, int Total, int Difficulty, IReadOnlyList<CheckLine> Breakdown);
