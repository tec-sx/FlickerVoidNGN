namespace FlickerVoid.Engine.Debug;

/// <summary>Reads the running process's figures, so statistics can be tested without a real process.</summary>
public interface IProcessProbe
{
    int ProcessorCount { get; }

    ProcessSample Read();
}
