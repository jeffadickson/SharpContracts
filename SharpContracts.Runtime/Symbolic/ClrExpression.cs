namespace SharpContracts.Symbolic;

/// <summary>CLR scalar identity, independent of Roslyn and the solver.</summary>
public sealed record ClrScalarType
{
    public bool IsBoolean { get; }
    public int Width { get; }
    public bool IsSigned { get; }
    private ClrScalarType(bool boolean, int width, bool signed)
        => (IsBoolean, Width, IsSigned) = (boolean, width, signed);
    public static ClrScalarType Boolean { get; } = new(true, 0, false);
    public static ClrScalarType Integral(int width, bool signed)
    {
        if (width is not (8 or 16 or 32 or 64))
            throw new ArgumentOutOfRangeException(nameof(width));
        return new(false, width, signed);
    }
}

public sealed record ClrSourceSpan(int Start, int Length, int Line, int Column);

/// <summary>Typed extension of the original symbolic AST; integer and real nodes remain separate models.</summary>
public abstract record ClrExpression(ClrScalarType ValueType)
    : ContractExpr(ValueType.IsBoolean ? SymbolicType.Boolean : SymbolicType.BitVector)
{
    public ClrSourceSpan? Source { get; init; }
}

public sealed record ClrConstant(ulong Bits, ClrScalarType ScalarType) : ClrExpression(ScalarType);
public sealed record ClrParameter(string Name, ClrScalarType ScalarType) : ClrExpression(ScalarType);
public enum ClrBinaryOperator
{
    Add, Subtract, Multiply, Divide, Equal, NotEqual,
    LessThan, LessOrEqual, GreaterThan, GreaterOrEqual, And, Or, Xor,
    ShiftLeft, ShiftRight, UnsignedShiftRight
}
public sealed record ClrBinary(ClrBinaryOperator Operator, ClrExpression Left,
    ClrExpression Right, ClrScalarType ResultType, bool IsChecked = false) : ClrExpression(ResultType);
public enum ClrUnaryOperator { Plus, Negate, LogicalNot, BitwiseNot }
public sealed record ClrUnary(ClrUnaryOperator Operator, ClrExpression Operand,
    ClrScalarType ResultType, bool IsChecked = false) : ClrExpression(ResultType);
public sealed record ClrConvert(ClrExpression Operand, ClrScalarType TargetType,
    bool IsChecked = false) : ClrExpression(TargetType);
