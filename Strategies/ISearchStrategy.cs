using System.Security.Claims;
using EgyptOnline.Dtos;
using Microsoft.AspNetCore.Mvc;


namespace EgyptOnline.Strategies
{
    public interface ISearchStrategy
    {
        Task<IActionResult> SearchAsync(FilterSearchDto filter, ClaimsPrincipal user);
    }

}
