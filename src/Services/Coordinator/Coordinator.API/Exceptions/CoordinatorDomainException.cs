using System;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Infrastructure.Exceptions;

/// <summary>
/// Exception type for app exceptions
/// </summary>
public class CoordinatorDomainException : Exception
{
    public CoordinatorDomainException()
    { }

    public CoordinatorDomainException(string message)
        : base(message)
    { }

    public CoordinatorDomainException(string message, Exception innerException)
        : base(message, innerException)
    { }
}
