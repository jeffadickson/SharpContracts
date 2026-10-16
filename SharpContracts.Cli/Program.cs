using SharpContracts.Verification;

bool json = args.Contains("--json", StringComparer.Ordinal);
bool inline = args.Contains("--inline", StringComparer.Ordinal);
string[] positions = args.Where(a => a != "--json" && a != "--inline").ToArray();
if(positions.Length != 2 || args.Length != positions.Length + (json ? 1 : 0) + (inline ? 1 : 0))
{
    Console.Error.WriteLine("Usage: SharpContracts.Cli [--inline] [--json] <source.cs> <methodName>");
    return 2;
}
try
{
    string source=await File.ReadAllTextAsync(positions[0]);
    MethodVerification verification=inline
        ? InlineContractVerifier.Verify(source, positions[1])
        : BoundNumericVerifier.Verify(source, positions[1]);
    ProofReport report=ProofReport.From(verification);
    if(json)
        Console.WriteLine(report.ToJson());
    else
    {
        Console.WriteLine("Outcome: "+report.Outcome);
        foreach(var error in report.Diagnostics) Console.Error.WriteLine("DIAGNOSTIC: "+error);
        foreach(var check in report.Checks)
        {
            Console.WriteLine($"{positions[0]}:{check.Line}: {check.ContractId}: {check.Status}");
            if(check.Counterexample is not null) Console.WriteLine(check.Counterexample);
            if(check.Reason is not null) Console.WriteLine(check.Reason);
        }
    }
    return report.ExitCode;
}
catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
