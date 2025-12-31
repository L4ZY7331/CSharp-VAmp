using Microsoft.Playwright;

namespace CSharpVAmp.Models;

public record ProxyInfo
{
    public string Server { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public Proxy? ToPlaywrightProxy()
    {
        if (string.IsNullOrEmpty(Server))
        {
            return null;
        }

        return new Proxy
        {
            Server = Server,
            Username = Username,
            Password = Password
        };
    }
}

