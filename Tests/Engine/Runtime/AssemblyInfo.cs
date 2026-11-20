// Arch registers component types in a static dictionary without a lock, so all tests share one collection and never run in parallel.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
