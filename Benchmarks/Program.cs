using BenchmarkDotNet.Running;

using FlickerVoid.Engine.Benchmarks.Rendering;

// Runs the benchmarks picked on the command line (--filter), or asks which to run; see BenchmarkDotNet's console arguments.
BenchmarkSwitcher switcher = BenchmarkSwitcher.FromAssembly(typeof(WorldExtractorBenchmarks).Assembly);
switcher.Run(args);
