using BenchmarkDotNet.Running;

// Runs every *Benchmarks class in the assembly. These measure the PRD's 50 MB load path, which is
// measured before anything is optimised. Run with:
//   dotnet run -c Release --project Plaintain.Benchmarks
//   just bench-plaintain
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

internal sealed partial class Program;
