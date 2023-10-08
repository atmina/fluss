namespace Fluss.Extensions;

public static class ValueTaskExtensions
{
    public static async ValueTask<bool> AnyAsync(this IEnumerable<ValueTask<bool>> valueTasks)
    {
        foreach (var valueTask in valueTasks)
        {
            if (await valueTask)
            {
                return true;
            }
        }

        return false;
    }

    public static async ValueTask<bool> AllAsync(this IEnumerable<ValueTask<bool>> valueTasks)
    {
        foreach (var valueTask in valueTasks)
        {
            if (!await valueTask)
            {
                return false;
            }
        }

        return true;
    }

    public static T GetResult<T>(this ValueTask<T> valueTask)
    {
        var task = Task.Run(async () => await valueTask);
        return task.GetAwaiter().GetResult();
    }

    public static void GetResult(this ValueTask valueTask)
    {
        var task = Task.Run(async () => await valueTask);
        task.GetAwaiter().GetResult();
    }
}
