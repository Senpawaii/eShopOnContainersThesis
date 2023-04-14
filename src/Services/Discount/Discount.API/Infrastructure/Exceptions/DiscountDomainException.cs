namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.Exceptions;

/// <summary>
/// Exception type for app exceptions
/// </summary>
public class DiscountDomainException : Exception
{
    public DiscountDomainException()
    { }

    public DiscountDomainException(string message)
        : base(message)
    { }

    public DiscountDomainException(string message, Exception innerException)
        : base(message, innerException)
    { }
}
