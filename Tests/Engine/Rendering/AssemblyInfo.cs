// One GPU device at a time: software drivers in CI are slow to share, and a lost device must not take other tests down.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
