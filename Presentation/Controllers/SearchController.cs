using EgyptOnline.Data;
using EgyptOnline.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SearchController(ApplicationDbContext context)
        {
            _context = context;
        }
        //Search for the workers based on the things that you want
        [HttpGet]
        public async Task<IActionResult> SearchWorkers([FromBody] FilterSearchDto? filter)
        {
            var workers = _context.Workers.AsQueryable();
            if (filter != null)
            {
                if (!string.IsNullOrEmpty(filter.FullName))
                {
                    workers = workers.Where(w => w.User.UserName.Contains(filter.FullName));
                }

                if (!string.IsNullOrEmpty(filter.Location))
                {
                    workers = workers.Where(w => w.Location != null && w.Location.Contains(filter.Location));
                }

                if (filter.Skills != null && filter.Skills.Any())
                {
                    //Skills logic
                }
            }

            var result = await workers.ToListAsync();
            return Ok(
                result.Select(
                w => new
                {
                    w.Id,
                    w.User.UserName,
                    w.User.Email,
                    w.Location,
                    w.IsAvailable,
                }
                ).ToList()
            );
        }
    }
}