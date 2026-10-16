namespace SharpContracts;

/// <summary>Requests build-time verification for a method within the supported C# subset.</summary>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class VerifyAttribute : System.Attribute
{
}
