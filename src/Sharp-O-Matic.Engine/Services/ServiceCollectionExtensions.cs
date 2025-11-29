namespace SharpOMatic.Engine.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharpOMaticJsonConverters(this IServiceCollection services, IEnumerable<Type> converterTypes)
    {
        services.AddSingleton(provider =>
        {
            List<JsonConverter> callerConverters = [];

            foreach(var ct in converterTypes)
            {
                if (!typeof(JsonConverter).IsAssignableFrom(ct))
                    throw new ArgumentException($"Type '{ct.FullName}' is not a JsonConverter.");

                if (Activator.CreateInstance(ct) is not JsonConverter converterInstance)
                    throw new ArgumentException($"Could not create instance of '{ct.FullName}'.");

                callerConverters.Add(converterInstance);
            }

            return (IEnumerable<JsonConverter>)callerConverters;
        });

        return services;
    }

    public static IServiceCollection AddSharpOMaticJsonConverters(this IServiceCollection services, params Type[] converterTypes)
        => services.AddSharpOMaticJsonConverters((IEnumerable<Type>)converterTypes);
}
