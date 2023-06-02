namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Infrastructure.Exceptions;

/// <summary>
/// Exception type for app exceptions
/// </summary>
public class ThesisFrontendDomainException : Exception {
    public ThesisFrontendDomainException() { }

    public ThesisFrontendDomainException(string message)
        : base(message) { }

    public ThesisFrontendDomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
