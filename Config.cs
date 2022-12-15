﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebSocks;

public record Config
{
    public string ListenOn { get; init; }
    public string TunnelTo { get; init; }
    public string Key { get; init; }
    public bool UseSystemProxy { get; init; } = true;

    internal Uri ListenUri => new(ListenOn);
    internal Uri TunnelUri => new(TunnelTo);
    internal SystemProxyConfig SystemProxyConfig => new(UseSystemProxy);

    internal static async Task<Config> Load(string path)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read);

        var opt = new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        var conf = await JsonSerializer.DeserializeAsync<Config>(file, opt);
        if (conf == null)
            throw new Exception("config cannot be null");

        return conf;
    }
}
