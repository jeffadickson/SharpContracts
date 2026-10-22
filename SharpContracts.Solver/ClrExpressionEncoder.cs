using Microsoft.Z3;
using SharpContracts.Symbolic;

namespace SharpContracts.Solver;

/// <summary>One fail-closed BV encoder for typed CLR expressions. No Roslyn dependency.</summary>
public sealed class ClrExpressionEncoder(Context context)
{
    private readonly Dictionary<string, ClrScalarType> parameterTypes = new(StringComparer.Ordinal);

    public Microsoft.Z3.Expr Encode(ClrExpression expression)
    {
        switch (expression)
        {
            case ClrConstant constant:
                if (constant.ValueType.IsBoolean)
                {
                    if (constant.Bits > 1) throw new ArgumentException("Invalid Boolean constant");
                    return context.MkBool(constant.Bits == 1);
                }
                if (constant.ValueType.Width < 64 && constant.Bits >= (1UL << constant.ValueType.Width))
                    throw new ArgumentException("Constant exceeds its bit-vector width");
                return context.MkBV(constant.Bits, (uint)constant.ValueType.Width);
            case ClrParameter parameter:
                if (string.IsNullOrWhiteSpace(parameter.Name)) throw new ArgumentException("Parameter name required");
                if (parameterTypes.TryGetValue(parameter.Name, out var previous) && previous != parameter.ValueType)
                    throw new ArgumentException("Conflicting parameter type: " + parameter.Name);
                parameterTypes[parameter.Name] = parameter.ValueType;
                return parameter.ValueType.IsBoolean ? context.MkBoolConst(parameter.Name)
                    : context.MkBVConst(parameter.Name, (uint)parameter.ValueType.Width);
            case ClrConvert conversion:
            {
                if (conversion.IsChecked) throw new NotSupportedException("Checked conversion requires exception verification");
                if (conversion.ValueType.IsBoolean || conversion.Operand.ValueType.IsBoolean)
                {
                    if (conversion.ValueType != conversion.Operand.ValueType)
                        throw new NotSupportedException("Boolean/numeric conversion unsupported");
                    return Encode(conversion.Operand);
                }
                var operand = (BitVecExpr)Encode(conversion.Operand);
                uint from = operand.SortSize, to = (uint)conversion.ValueType.Width;
                if (from == to) return operand;
                if (to < from) return context.MkExtract(to - 1, 0, operand);
                return conversion.Operand.ValueType.IsSigned
                    ? context.MkSignExt(to - from, operand) : context.MkZeroExt(to - from, operand);
            }
            case ClrUnary unary:
            {
                if (unary.IsChecked) throw new NotSupportedException("Checked unary arithmetic requires exception verification");
                if (unary.ValueType != unary.Operand.ValueType) throw new ArgumentException("Unary type mismatch");
                var operand = Encode(unary.Operand);
                return unary.Operator switch
                {
                    ClrUnaryOperator.LogicalNot when operand is BoolExpr b => context.MkNot(b),
                    ClrUnaryOperator.Plus when operand is BitVecExpr => operand,
                    ClrUnaryOperator.Negate when operand is BitVecExpr v => context.MkBVNeg(v),
                    ClrUnaryOperator.BitwiseNot when operand is BitVecExpr v => context.MkBVNot(v),
                    _ => throw new NotSupportedException("Unsupported unary expression")
                };
            }
            case ClrBinary binary:
                return EncodeBinary(binary);
            default: throw new NotSupportedException("Unsupported CLR expression node");
        }
    }

    private Microsoft.Z3.Expr EncodeBinary(ClrBinary binary)
    {
        if (binary.IsChecked) throw new NotSupportedException("Checked arithmetic requires exception verification");
        if (binary.Left.ValueType != binary.Right.ValueType) throw new ArgumentException("Binary operand type mismatch");
        bool comparison = binary.Operator is ClrBinaryOperator.Equal or ClrBinaryOperator.NotEqual
            or ClrBinaryOperator.LessThan or ClrBinaryOperator.LessOrEqual
            or ClrBinaryOperator.GreaterThan or ClrBinaryOperator.GreaterOrEqual;
        if (binary.ValueType != (comparison ? ClrScalarType.Boolean : binary.Left.ValueType))
            throw new ArgumentException("Binary result type mismatch");
        // C# division exceptions cannot be replaced by SMT's total division semantics.
        if (binary.Operator == ClrBinaryOperator.Divide &&
            (binary.Left.ValueType.IsBoolean || binary.Left.ValueType.IsSigned ||
             binary.Right is not ClrConstant { Bits: > 0 }))
            throw new NotSupportedException("Division requires unsigned operands and a nonzero constant divisor");
        var left = Encode(binary.Left);
        var right = Encode(binary.Right);
        if (binary.Operator == ClrBinaryOperator.Equal) return context.MkEq(left, right);
        if (binary.Operator == ClrBinaryOperator.NotEqual) return context.MkNot(context.MkEq(left, right));
        if (left is BoolExpr lb && right is BoolExpr rb)
            return binary.Operator switch
            {
                ClrBinaryOperator.And => context.MkAnd(lb, rb),
                ClrBinaryOperator.Or => context.MkOr(lb, rb),
                ClrBinaryOperator.Xor => context.MkXor(lb, rb),
                _ => throw new NotSupportedException("Unsupported Boolean operator")
            };
        if (left is not BitVecExpr a || right is not BitVecExpr b)
            throw new ArgumentException("Expected bit-vector operands");
        bool signed = binary.Left.ValueType.IsSigned;
        return binary.Operator switch
        {
            ClrBinaryOperator.Add => context.MkBVAdd(a, b),
            ClrBinaryOperator.Subtract => context.MkBVSub(a, b),
            ClrBinaryOperator.Multiply => context.MkBVMul(a, b),
            ClrBinaryOperator.Divide => context.MkBVUDiv(a, b),
            ClrBinaryOperator.And => context.MkBVAND(a, b),
            ClrBinaryOperator.Or => context.MkBVOR(a, b),
            ClrBinaryOperator.Xor => context.MkBVXOR(a, b),
            ClrBinaryOperator.LessThan => signed ? context.MkBVSLT(a, b) : context.MkBVULT(a, b),
            ClrBinaryOperator.LessOrEqual => signed ? context.MkBVSLE(a, b) : context.MkBVULE(a, b),
            ClrBinaryOperator.GreaterThan => signed ? context.MkBVSGT(a, b) : context.MkBVUGT(a, b),
            ClrBinaryOperator.GreaterOrEqual => signed ? context.MkBVSGE(a, b) : context.MkBVUGE(a, b),
            _ => throw new NotSupportedException("Unsupported bit-vector operator")
        };
    }
}
