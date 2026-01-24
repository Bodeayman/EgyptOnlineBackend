using EgyptOnline.Dtos;
using EgyptOnline.Models;
using Microsoft.AspNetCore.Mvc;

namespace EgyptOnline.Strategies
{
    /// <summary>
    /// Strategy interface for registering different types of service providers.
    /// Each provider type (Worker, Assistant, Contractor, etc.) implements this interface.
    /// </summary>
    public interface IProviderRegistrationStrategy
    {
        /// <summary>
        /// Validates the registration model for the specific provider type.
        /// </summary>
        /// <returns>Validation result with error message if invalid; null if valid.</returns>
        string? Validate(RegisterWorkerDto model);

        /// <summary>
        /// Creates and returns the appropriate service provider object.
        /// </summary>
        ServicesProvider CreateProvider(RegisterWorkerDto model, User user);
    }
}
