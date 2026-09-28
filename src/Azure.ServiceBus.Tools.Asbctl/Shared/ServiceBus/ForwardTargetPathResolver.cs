namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public static class ForwardTargetPathResolver
{
    public static string NormalizeEntityName(string forwardTarget)
    {
        var raw = forwardTarget.Trim();
        var path = raw;

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            path = uri.AbsolutePath;
        }

        path = path.Trim().Trim('/');

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Forward target is empty.");
        }

        if (path.Contains('/'))
        {
            throw new ArgumentException(
                $"Unsupported forward target '{forwardTarget}'. Only queue/topic entity names are supported.");
        }

        return path;
    }
}
