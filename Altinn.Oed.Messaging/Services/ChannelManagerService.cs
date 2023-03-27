using System.Collections.Concurrent;
using System.ServiceModel;
using Altinn.Oed.Messaging.Exceptions;
using Altinn.Oed.Messaging.Models.Interfaces;
using Altinn.Oed.Messaging.Services.Interfaces;

namespace Altinn.Oed.Messaging.Services;

public class ChannelManagerService : IChannelManagerService
{
    private readonly string _url;
    private readonly ConcurrentDictionary<Type, string> _endpoints = new();
    private readonly ConcurrentDictionary<Type, object> _channels = new();

    public ChannelManagerService(IOedNotificationSettings settings)
    {
        _url = settings.AltinnServiceAddress;
    }

    public void Add<T>(string endpoint)
    {
        _endpoints.TryAdd(typeof(T), endpoint);
    }

    public T Get<T>() where T : class
    {
        var type = typeof(T);
        if (!_endpoints.ContainsKey(type))
            throw new MessagingServiceException($"Unknown type: {type.Name}");
        if (!_channels.ContainsKey(type))
            _channels.TryAdd(type, ClientFactory<T>(_url + _endpoints[type]));
        return (T)_channels[type];
    }

    public async Task<object> With<T>(Func<T, Task<object>> func) where T : class
    {
        var obj = Get<T>();
        try
        {
            var res = await func.Invoke(obj);
            return res;
        }
        catch (FaultException<ExternalServices.Correspondence.AltinnFault> ex)
        {
            var methodName = typeof(T) + "." + func;
            throw CreateNotificationServiceException(methodName, ex.Detail.AltinnErrorMessage, ex);
        }
    }

    private static MessagingServiceException CreateNotificationServiceException(string methodName, string msg, Exception ex)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var message = $"[{timestamp}] Error, {methodName}: {msg}";
        throw new MessagingServiceException(message, ex);
    }

    private T ClientFactory<T>(string endpoint)
    {
        BasicHttpBinding basicBinding = new BasicHttpBinding
        {
            SendTimeout = TimeSpan.FromSeconds(60),
            OpenTimeout = TimeSpan.FromSeconds(60),
            MaxReceivedMessageSize = 65536 * 1024,
        };

        if (endpoint.StartsWith("https://"))
        {
            basicBinding.Security.Mode = BasicHttpSecurityMode.Transport;
        }

        var obj = (T)Activator.CreateInstance(typeof(T), basicBinding, new EndpointAddress(new Uri(endpoint)))!;

        return obj;
    }
}
