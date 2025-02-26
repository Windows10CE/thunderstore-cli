using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThunderstoreCLI.Models;

public abstract class BaseJson<T, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Context>
    where T : BaseJson<T, Context>
    where Context : JsonSerializerContext
{
    public string Serialize(JsonSerializerOptions? options = null)
    {
        var context = (Context) Activator.CreateInstance(typeof(Context), options)!;

        return JsonSerializer.Serialize(this, typeof(T), context);
    }
    public static T? Deserialize(string json, JsonSerializerOptions? options = null)
    {
        var context = (Context) Activator.CreateInstance(typeof(Context), options)!;

        return (T?) JsonSerializer.Deserialize(json, typeof(T), context);
    }
    public static T? Deserialize(Stream json, JsonSerializerOptions? options)
    {
        var context = (Context) Activator.CreateInstance(typeof(Context), options)!;

        return (T?) JsonSerializer.Deserialize(json, typeof(T), context);
    }
}
