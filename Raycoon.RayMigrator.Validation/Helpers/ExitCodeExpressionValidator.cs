namespace Raycoon.RayMigrator.Validation.Helpers;

/// <summary>
/// Sole source of truth for parsing and validating CLI exit-code expressions.
/// Supports: single value (<c>"0"</c>), closed range (<c>"1..5"</c>),
/// open-up range (<c>"1.."</c>), and open-down range (<c>"..-1"</c>).
/// </summary>
public static class ExitCodeExpressionValidator
{
    /// <summary>
    /// Validates a single expression. Returns true on success, or false with a descriptive
    /// <paramref name="errorMessage"/> on failure.
    /// </summary>
    public static bool IsValid(string? expression, out string? errorMessage)
    {
        var expr = expression?.Trim();

        if (string.IsNullOrEmpty(expr))
        {
            errorMessage = "Empty expression is not allowed.";
            return false;
        }

        int dotDotIndex = expr.IndexOf("..", StringComparison.Ordinal);

        if (dotDotIndex < 0)
        {
            if (!int.TryParse(expr, out _))
            {
                errorMessage = $"'{expr}' is not a valid integer.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        string leftPart = expr[..dotDotIndex].Trim();
        string rightPart = expr[(dotDotIndex + 2)..].Trim();

        bool hasLeft = leftPart.Length > 0;
        bool hasRight = rightPart.Length > 0;

        if (!hasLeft && !hasRight)
        {
            errorMessage = "'..' (unbounded range) is not allowed. Use 'min..max', 'min..', or '..max'.";
            return false;
        }

        int leftVal = 0, rightVal = 0;

        if (hasLeft && !int.TryParse(leftPart, out leftVal))
        {
            errorMessage = $"'{leftPart}' (left side of '..') is not a valid integer.";
            return false;
        }

        if (hasRight && !int.TryParse(rightPart, out rightVal))
        {
            errorMessage = $"'{rightPart}' (right side of '..') is not a valid integer.";
            return false;
        }

        if (hasLeft && hasRight && leftVal > rightVal)
        {
            errorMessage = $"Reversed range '{expr}' (min {leftVal} > max {rightVal}). Use '{rightVal}..{leftVal}' instead.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Validates all expressions. Returns null on success, or the first encountered error message on failure.
    /// Also returns null when <paramref name="expressions"/> is null or empty (caller decides the default).
    /// </summary>
    public static string? ValidateAll(IEnumerable<string>? expressions)
    {
        if (expressions is null) return null;

        foreach (var expr in expressions)
        {
            if (!IsValid(expr, out var err))
                return err;
        }
        return null;
    }
}
