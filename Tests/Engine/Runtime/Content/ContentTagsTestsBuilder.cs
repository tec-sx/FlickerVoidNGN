using System.Text;

using FlickerVoid.Engine.Runtime.Content;
using FlickerVoid.Engine.Runtime.Loop;

using Microsoft.Extensions.Logging.Abstractions;

namespace FlickerVoid.Engine.Runtime.Tests.Content;

/// <summary>A game that registers a <c>lock</c> component and an <c>errand</c> data asset with <c>fetch</c> steps, and the tags of its content.</summary>
internal sealed class ContentTagsTestsBuilder : IDisposable
{
    private readonly Game _game = new(NullLoggerFactory.Instance);

    public ContentTagsTestsBuilder()
    {
        _game.Spawner.Register("lock", ContentTagsTestsJsonContext.Default.Lock);
        _game.Data.AddResolver(ContentTagsTestsJsonContext.Default);
        _game.Data.Register<Errand>("errand");
        _game.Data.RegisterDerived<Step, FetchStep>("fetch");
        Tags = new ContentTags(_game);
    }

    public ContentTags Tags { get; }

    public static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(json);

    public static string Text(byte[] json) => Encoding.UTF8.GetString(json);

    public void Dispose() => _game.Dispose();
}
