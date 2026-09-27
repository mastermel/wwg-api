using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>Data Protection keys kept in memory, so tests never write key files.</summary>
internal sealed class InMemoryXmlRepository : IXmlRepository
{
    private readonly List<XElement> _elements = [];
    private readonly Lock _lock = new();

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        lock (_lock)
        {
            return [.. _elements.Select(e => new XElement(e))];
        }
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        lock (_lock)
        {
            _elements.Add(new XElement(element));
        }
    }
}
