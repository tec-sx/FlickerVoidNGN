using System.Reflection;
using System.Text.Json.Serialization.Metadata;

namespace FlickerVoid.Editor.Core.Inspector;

/// <summary>Reads the attributes a type declares on a property through its source-generated metadata.</summary>
public static class PropertyAttributes
{
    public static TAttribute? Find<TAttribute>(JsonPropertyInfo property)
        where TAttribute : Attribute
    {
        ICustomAttributeProvider? provider = property.AttributeProvider;
        if (provider is null)
        {
            return null;
        }

        object[] found = provider.GetCustomAttributes(typeof(TAttribute), inherit: true);
        if (found.Length == 0)
        {
            return null;
        }

        return (TAttribute)found[0];
    }
}
