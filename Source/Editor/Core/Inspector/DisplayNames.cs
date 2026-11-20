using System.Text;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>Turns identifiers such as <c>castsShadows</c> or <c>PointLight</c> into labels such as "Casts Shadows".</summary>
public static class DisplayNames
{
    public static string FromIdentifier(string identifier)
    {
        StringBuilder label = new(identifier.Length + 4);
        for (int index = 0; index < identifier.Length; index++)
        {
            char character = identifier[index];
            if (index == 0)
            {
                label.Append(char.ToUpperInvariant(character));
                continue;
            }

            char previous = identifier[index - 1];
            bool startsWord = char.IsUpper(character) && (char.IsLower(previous) || char.IsDigit(previous));
            if (startsWord)
            {
                label.Append(' ');
            }

            label.Append(character);
        }

        return label.ToString();
    }
}
