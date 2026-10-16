using SharpContracts.Verification;

bool json = args.Length == 3 && args[0] == "--json";
int offset = json ? 1 : 0;
if(args.Length-offset != 2)
{
    Console.Error.WriteLine("Usage: SharpContracts.Cli [--json] <source.cs> <methodName>");
    return 2;
}
try
{
    string source=await File.ReadAllTextAsync(args[offset]);
    ProofReport report=ProofReport.From(BoundNumericVerifier.Verify(source,args[offset+1]));
    if(json)
        Console.WriteLine(report.ToJson());
    else
    {
        Console.WriteLine("Outcome: "+report.Outcome);
        foreach(var error in report.Diagnostics) Console.Error.WriteLine("DIAGNOSTIC: "+error);
        foreach(var check in report.Checks)
        {
            Console.WriteLine($"{args[offset]}:{check.Line}: {check.ContractId}: {check.Status}");
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
