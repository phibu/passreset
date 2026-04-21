using System.DirectoryServices.Protocols;
using PassReset.PasswordProvider.Ldap;

namespace PassReset.Tests.Fakes;

/// <summary>
/// Scripted <see cref="ILdapSession"/> fake for unit + contract tests.
/// Callers register <see cref="SearchResponse"/>/<see cref="ModifyResponse"/> values
/// (or exceptions to throw) keyed by operation type + filter substring.
/// </summary>
public sealed class FakeLdapSession : ILdapSession
{
    private readonly List<SearchRule> _searchRules = new();
    private readonly List<ModifyRule> _modifyRules = new();

    public SearchResultEntry? RootDse { get; set; }

    public int SearchCallCount { get; private set; }
    public int ModifyCallCount { get; private set; }
    public int BindCallCount { get; private set; }

    public Exception? BindThrows { get; set; }

    public void Bind()
    {
        BindCallCount++;
        if (BindThrows is not null) throw BindThrows;
    }

    public FakeLdapSession OnSearch(string filterContains, SearchResponse response)
    {
        _searchRules.Add(new SearchRule(filterContains, response, null));
        return this;
    }

    public FakeLdapSession OnSearchThrow(string filterContains, Exception ex)
    {
        _searchRules.Add(new SearchRule(filterContains, null, ex));
        return this;
    }

    public FakeLdapSession OnModify(string dnContains, ModifyResponse response)
    {
        _modifyRules.Add(new ModifyRule(dnContains, response, null));
        return this;
    }

    public FakeLdapSession OnModifyThrow(string dnContains, Exception ex)
    {
        _modifyRules.Add(new ModifyRule(dnContains, null, ex));
        return this;
    }

    public SearchResponse Search(SearchRequest request)
    {
        SearchCallCount++;
        foreach (var rule in _searchRules)
        {
            if (request.Filter is string f && f.Contains(rule.FilterContains, StringComparison.OrdinalIgnoreCase))
            {
                if (rule.Throw is not null) throw rule.Throw;
                return rule.Response!;
            }
        }
        throw new InvalidOperationException(
            $"FakeLdapSession: no matching SearchRule for filter='{request.Filter}'. Register one via OnSearch(...).");
    }

    public ModifyResponse Modify(ModifyRequest request)
    {
        ModifyCallCount++;
        foreach (var rule in _modifyRules)
        {
            if (request.DistinguishedName.Contains(rule.DnContains, StringComparison.OrdinalIgnoreCase))
            {
                if (rule.Throw is not null) throw rule.Throw;
                return rule.Response!;
            }
        }
        throw new InvalidOperationException(
            $"FakeLdapSession: no matching ModifyRule for DN='{request.DistinguishedName}'. Register one via OnModify(...).");
    }

    public void Dispose() { }

    private sealed record SearchRule(string FilterContains, SearchResponse? Response, Exception? Throw);
    private sealed record ModifyRule(string DnContains, ModifyResponse? Response, Exception? Throw);
}
