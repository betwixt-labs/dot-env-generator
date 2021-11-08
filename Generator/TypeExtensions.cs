using System;

namespace DotEnvGenerator;

internal static class TypeExtensions
{
    /// <summary>
    /// Determines if a type can be use with the `const` keyword.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public static bool CanBeConsts(this Type type) => type.IsNumericType() || type == typeof(string) || type == typeof(bool);

    /// <summary>
    ///     Determines if a type is numeric. 
    /// </summary>
    /// <remarks>
    ///     Boolean is not considered numeric. Nullable numeric types are considered numeric.
    /// </remarks>
    public static bool IsNumericType(this Type? type)
    {
        if (type is null)
        {
            return false;
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Byte:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.SByte:
            case TypeCode.Single:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
                return true;
            case TypeCode.Object:
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    return IsNumericType(Nullable.GetUnderlyingType(type));
                }
                return false;
        }
        return false;
    }
}