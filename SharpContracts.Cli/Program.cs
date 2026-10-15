using SharpContracts.Verification;
using SharpContracts.Solver;

if (args.Length != 2 || args[0] is "--help" or "-h")
{
    Console.Error.WriteLine("Usage: dotnet run --project SharpContracts.Cli -- <source.cs> <methodName>");
    return 2;
}

try
{
    string source = await File.ReadAllTextAsync(args[0]);
    MethodVerification report = BoundNumericVerifier.Verify(source, args[1]);
    foreach (string error in report.Errors)
        Console.Error.WriteLine("UNSUPPORTED/ERROR: " + error);
    foreach (var check in report.Checks)
    {
        Console.WriteLine($"{args[0]}:{check.Line}: {check.ContractId}: {check.Status}");
        if (check.Counterexample is not null)
            Console.WriteLine("Counterexample: " + check.Counterexample);
        if (check.Reason is not null)
            Console.WriteLine("Reason: " + check.Reason);
    }
    if (report.AllProved)
    {
        Console.WriteLine("ALL CHECKS PROVED under the supported bit-vector model.");
        return 0;
    }
    if (report.Errors.Count != 0 || report.Checks.Count == 0)
        return 2; // unsupported, invalid, or no contracts
    return 1; // counterexample or unknown
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(e.Message);
    return 2;
}
