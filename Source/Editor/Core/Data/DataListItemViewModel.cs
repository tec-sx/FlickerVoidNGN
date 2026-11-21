namespace FlickerVoid.Editor.Core.Data;

/// <summary>One object of a data list, such as a fragment, with its form.</summary>
public sealed class DataListItemViewModel(DataListViewModel list, DataObjectViewModel form)
{
    public DataListViewModel List { get; } = list;

    public DataObjectViewModel Form { get; } = form;
}
