namespace Raycoon.RayMigrator.Core.Extensions;

public static class ExceptionExtensions
{
    public static string GetExceptionDetails(this Exception exception)
    {
        if (exception == null) return string.Empty;

        // Base information of the top-level exception
        var messageBuilder = new System.Text.StringBuilder();
        messageBuilder.AppendLine("\nException Details:");
        messageBuilder.AppendLine($"Type: {exception.GetType().FullName}");
        messageBuilder.AppendLine($"Message: {exception.Message}");
        messageBuilder.AppendLine($"StackTrace:\n{exception.StackTrace}");

        // Recursively traverse all inner exceptions
        if (exception.InnerException != null)
        {
            messageBuilder.Append(GetInnerExceptionDetails(exception.InnerException, 1));
        }
        return messageBuilder.ToString();
    }

    private static string GetInnerExceptionDetails(Exception exception, int depth)
    {
        if (exception == null) return string.Empty;

        var indent = new string(' ', depth * 4); // Generate indentation based on nesting depth
        var messageBuilder = new System.Text.StringBuilder();
        messageBuilder.AppendLine($"\n{indent}Inner Exception:");
        messageBuilder.AppendLine($"{indent}Type: {exception.GetType().FullName}");
        messageBuilder.AppendLine($"{indent}Message: {exception.Message}");
        messageBuilder.AppendLine($"{indent}StackTrace:\n{indent}{exception.StackTrace}");

        // Recursive call for the next inner exception, if present
        if (exception.InnerException != null)
        {
            messageBuilder.Append(GetInnerExceptionDetails(exception.InnerException, depth + 1));
        }
        return messageBuilder.ToString();
    }
}
