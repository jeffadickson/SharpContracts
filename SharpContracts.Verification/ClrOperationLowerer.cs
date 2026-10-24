using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using SharpContracts.Symbolic;

namespace SharpContracts.Verification;

/// <summary>Canonical semantic expression frontend shared by both compiler-bound entry points.</summary>
internal sealed class ClrOperationLowerer(IReadOnlyDictionary<ISymbol, ClrExpression> values)
{
    internal static ClrScalarType ScalarType(ITypeSymbol? symbol)
    {
        if (symbol?.SpecialType == SpecialType.System_Boolean) return ClrScalarType.Boolean;
        if (symbol is null) throw new NotSupportedException("Missing bound scalar type");
        var descriptor = NumericTypeDescriptor.FromSymbol(symbol);
        if (descriptor.Width is not int width || descriptor.Kind is not
            (NumericModelKind.SignedBitVector or NumericModelKind.UnsignedBitVector))
            throw new NotSupportedException("Unsupported scalar type: " + symbol);
        return ClrScalarType.Integral(width, descriptor.Kind == NumericModelKind.SignedBitVector);
    }

    internal ClrExpression Lower(IOperation operation)
    {
        ClrExpression expression = operation switch
        {
            IParenthesizedOperation p => Lower(p.Operand),
            IParameterReferenceOperation p when values.TryGetValue(p.Parameter, out var value) => value,
            ILocalReferenceOperation l when values.TryGetValue(l.Local, out var value) => value,
            ILiteralOperation literal => Literal(literal),
            IConversionOperation conversion => Conversion(conversion),
            IUnaryOperation unary => Unary(unary),
            IBinaryOperation binary => Binary(binary),
            _ => throw new NotSupportedException("Unsupported bound operation " + operation.Kind)
        };
        var line = operation.Syntax.GetLocation().GetLineSpan().StartLinePosition;
        return expression with { Source = new(operation.Syntax.SpanStart, operation.Syntax.Span.Length,
            line.Line + 1, line.Character + 1) };
    }

    private static ClrExpression Literal(ILiteralOperation literal)
    {
        var type = ScalarType(literal.Type);
        ulong bits = literal.ConstantValue.Value switch
        {
            bool b => b ? 1UL : 0UL,
            ulong n => n, long n => unchecked((ulong)n),
            uint n => n, int n => unchecked((ulong)n),
            ushort n => n, short n => unchecked((ulong)n),
            byte n => n, sbyte n => unchecked((ulong)n), char n => n,
            _ => throw new NotSupportedException("Unsupported literal value")
        };
        if (!type.IsBoolean && type.Width < 64) bits &= (1UL << type.Width) - 1;
        return new ClrConstant(bits, type);
    }

    private ClrExpression Conversion(IConversionOperation conversion)
    {
        if (conversion.IsChecked || conversion.OperatorMethod is not null ||
            (!conversion.Conversion.IsNumeric && !conversion.Conversion.IsIdentity &&
             !conversion.ConstantValue.HasValue))
            throw new NotSupportedException("Checked, user-defined or nonnumeric conversion unsupported");
        var operand = Lower(conversion.Operand);
        var target = ScalarType(conversion.Type);
        return operand.ValueType == target ? operand : new ClrConvert(operand, target);
    }

    private ClrExpression Unary(IUnaryOperation unary)
    {
        if (unary.IsChecked || unary.OperatorMethod is not null || unary.IsLifted)
            throw new NotSupportedException("Checked, lifted or custom unary operator unsupported");
        var op = unary.OperatorKind switch
        {
            UnaryOperatorKind.Plus => ClrUnaryOperator.Plus,
            UnaryOperatorKind.Minus => ClrUnaryOperator.Negate,
            UnaryOperatorKind.Not => ClrUnaryOperator.LogicalNot,
            UnaryOperatorKind.BitwiseNegation => ClrUnaryOperator.BitwiseNot,
            _ => throw new NotSupportedException("Unsupported unary operator " + unary.OperatorKind)
        };
        return new ClrUnary(op, Lower(unary.Operand), ScalarType(unary.Type));
    }

    private ClrExpression Binary(IBinaryOperation binary)
    {
        if (binary.IsChecked || binary.OperatorMethod is not null || binary.IsLifted)
            throw new NotSupportedException("Checked, lifted or custom binary operator unsupported");
        var left = Lower(binary.LeftOperand);
        var right = Lower(binary.RightOperand);
        var op = binary.OperatorKind switch
        {
            BinaryOperatorKind.Add => ClrBinaryOperator.Add,
            BinaryOperatorKind.Subtract => ClrBinaryOperator.Subtract,
            BinaryOperatorKind.Multiply => ClrBinaryOperator.Multiply,
            BinaryOperatorKind.Divide => ClrBinaryOperator.Divide,
            BinaryOperatorKind.Equals => ClrBinaryOperator.Equal,
            BinaryOperatorKind.NotEquals => ClrBinaryOperator.NotEqual,
            BinaryOperatorKind.LessThan => ClrBinaryOperator.LessThan,
            BinaryOperatorKind.LessThanOrEqual => ClrBinaryOperator.LessOrEqual,
            BinaryOperatorKind.GreaterThan => ClrBinaryOperator.GreaterThan,
            BinaryOperatorKind.GreaterThanOrEqual => ClrBinaryOperator.GreaterOrEqual,
            BinaryOperatorKind.And or BinaryOperatorKind.ConditionalAnd => ClrBinaryOperator.And,
            BinaryOperatorKind.Or or BinaryOperatorKind.ConditionalOr => ClrBinaryOperator.Or,
            BinaryOperatorKind.ExclusiveOr => ClrBinaryOperator.Xor,
            BinaryOperatorKind.LeftShift => ClrBinaryOperator.ShiftLeft,
            BinaryOperatorKind.RightShift => ClrBinaryOperator.ShiftRight,
            BinaryOperatorKind.UnsignedRightShift => ClrBinaryOperator.UnsignedShiftRight,
            _ => throw new NotSupportedException("Unsupported binary operator " + binary.OperatorKind)
        };
        if (op == ClrBinaryOperator.Divide)
        {
            if (left.ValueType.IsSigned || binary.RightOperand.ConstantValue.Value is not (uint or ulong) ||
                Convert.ToUInt64(binary.RightOperand.ConstantValue.Value) == 0)
                throw new NotSupportedException("Division requires a nonzero constant unsigned divisor");
            // Constant expressions can be bound arithmetic nodes; preserve purity checks above,
            // then materialize the compiler's constant so the backend can validate totality.
            right = new ClrConstant(Convert.ToUInt64(binary.RightOperand.ConstantValue.Value), right.ValueType)
                { Source = right.Source };
        }
        return new ClrBinary(op, left, right, ScalarType(binary.Type));
    }
}
