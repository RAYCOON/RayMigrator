using Raycoon.RayMigrator.Validation.Helpers;

namespace Raycoon.RayMigrator.Core.Configuration.Options;

/// <summary>
/// Matches process exit codes against a whitelist of expressions using C# range notation.
/// Supported formats: "0" (single value), "1..5" (closed range), "1.." (open-ended up), "..-1" (open-ended down).
/// </summary>
/// <remarks>
/// Syntactic validation is delegated to <see cref="ExitCodeExpressionValidator"/> (sole source of truth
/// in the WASM-safe <c>Raycoon.RayMigrator.Validation</c> library). Predicate construction stays here
/// because building <see cref="Func{TResult, T}"/> delegates is an engine/runtime concern.
/// </remarks>
public sealed class ExitCodeMatcher
{
    private readonly Func<int, bool>[] _predicates;
    private readonly string[] _expressions;

    private ExitCodeMatcher(Func<int, bool>[] predicates, string[] expressions)
    {
        _predicates = predicates;
        _expressions = expressions;
    }

    /// <summary>
    /// Default matcher that accepts only exit code 0.
    /// </summary>
    public static ExitCodeMatcher Default { get; } = new(
        new Func<int, bool>[] { code => code == 0 },
        new[] { "0" });

    /// <summary>
    /// Returns true if the given exit code matches any expression in the whitelist.
    /// </summary>
    public bool IsMatch(int exitCode)
    {
        for (int i = 0; i < _predicates.Length; i++)
        {
            if (_predicates[i](exitCode))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to parse an array of exit code expressions into an <see cref="ExitCodeMatcher"/>.
    /// Returns <see cref="Default"/> for null or empty input.
    /// </summary>
    public static bool TryParse(string[]? expressions, out ExitCodeMatcher matcher, out string? errorMessage)
    {
        if (expressions == null || expressions.Length == 0)
        {
            matcher = Default;
            errorMessage = null;
            return true;
        }

        // Single-source validation via the shared library. Any syntax error surfaces here with
        // an index-annotated message for backwards compatibility with pre-existing callers/tests.
        for (int i = 0; i < expressions.Length; i++)
        {
            if (!ExitCodeExpressionValidator.IsValid(expressions[i], out var err))
            {
                matcher = Default;
                errorMessage = $"SuccessExitCodes[{i}]: {err}";
                return false;
            }
        }

        // All expressions validated — safe to build predicates from the already-validated strings.
        var predicates = new List<Func<int, bool>>(expressions.Length);
        var normalized = new List<string>(expressions.Length);

        foreach (var raw in expressions)
        {
            var expr = raw.Trim();
            int dotDotIndex = expr.IndexOf("..", StringComparison.Ordinal);

            if (dotDotIndex < 0)
            {
                int single = int.Parse(expr);
                int captured = single;
                predicates.Add(code => code == captured);
                normalized.Add(expr);
                continue;
            }

            string leftPart = expr[..dotDotIndex].Trim();
            string rightPart = expr[(dotDotIndex + 2)..].Trim();

            bool hasLeft = leftPart.Length > 0;
            bool hasRight = rightPart.Length > 0;

            if (hasLeft && hasRight)
            {
                int min = int.Parse(leftPart);
                int max = int.Parse(rightPart);
                predicates.Add(code => code >= min && code <= max);
                normalized.Add($"{min}..{max}");
            }
            else if (hasLeft)
            {
                int min = int.Parse(leftPart);
                predicates.Add(code => code >= min);
                normalized.Add($"{min}..");
            }
            else
            {
                int max = int.Parse(rightPart);
                predicates.Add(code => code <= max);
                normalized.Add($"..{max}");
            }
        }

        matcher = new ExitCodeMatcher(predicates.ToArray(), normalized.ToArray());
        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Returns a human-readable representation for logging, e.g. "[0, 1..5]".
    /// </summary>
    public override string ToString() => $"[{string.Join(", ", _expressions)}]";
}
