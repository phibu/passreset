using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PassReset.Web.Services.Configuration;

/// <summary>
/// Reads and writes the encrypted <c>secrets.dat</c> file. The on-disk format is a
/// single <see cref="IConfigProtector.Protect"/>-wrapped JSON document; partial writes
/// are prevented via write-to-tmp + rename.
/// </summary>
internal sealed class SecretStore : ISecretStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    private readonly IConfigProtector _protector;
    private readonly string _path;
    private readonly ILogger<SecretStore> _log;

    public SecretStore(IConfigProtector protector, string path, ILogger<SecretStore> log)
    {
        _protector = protector;
        _path = path;
        _log = log;
    }

    public SecretBundle Load()
    {
        if (!File.Exists(_path))
        {
            _log.LogInformation("SecretStore: no file at {Path}; returning empty bundle", _path);
            return SecretBundle.Empty;
        }

        var ciphertext = File.ReadAllText(_path);
        var plaintext = _protector.Unprotect(ciphertext);
        var bundle = JsonSerializer.Deserialize<SecretBundle>(plaintext, JsonOpts);
        if (bundle is null)
        {
            _log.LogWarning("SecretStore: deserialization returned null; using empty bundle");
            return SecretBundle.Empty;
        }
        return bundle;
    }

    public void Save(SecretBundle bundle)
    {
        var plaintext = JsonSerializer.Serialize(bundle, JsonOpts);
        var ciphertext = _protector.Protect(plaintext);

        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, ciphertext);
        File.Move(tmp, _path, overwrite: true);

        _log.LogInformation("SecretStore: wrote {Path}", _path);
    }
}
