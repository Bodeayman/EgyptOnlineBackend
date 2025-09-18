using System.Linq.Expressions;
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
        [HttpPost("worker")]
        public async Task<IActionResult> SearchWorkers([FromBody] FilterSearchDto? filter)
        {
            try
            {

                var workers = _context.Workers.Include(w => w.User).AsQueryable();
                foreach (var w in workers)
                {
                    Console.WriteLine(w.User.UserName);
                }
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        workers = workers.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }

                    if (!string.IsNullOrEmpty(filter.Location))
                    {
                        workers = workers.Where(w => w.Location != null && w.Location.Contains(filter.Location));
                    }

                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        workers = workers.Where(w => w.Skill != null && w.Skill.Contains(filter.Profession));
                    }
                }

                var result = await workers.ToListAsync();
                return Ok(

                result.Select(
                w => new
                {
                    w.User.UserName,
                    w.User.Email,
                    w.User.PhoneNumber,
                    w.IsAvailable,
                    w.Location,
                    w.Skill,
                    w.Bio,
                    w.ProviderType,
                }
                )

                .ToList()

                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }
        [HttpPost("companies")]
        public async Task<IActionResult> SearchCompanies([FromBody] FilterSearchDto? filter)
        {
            try
            {

                var companies = _context.Companies.Include(w => w.User).AsQueryable();
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        companies = companies.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }

                    if (!string.IsNullOrEmpty(filter.Location))
                    {
                        companies = companies.Where(w => w.Location != null && w.Location.Contains(filter.Location));
                    }

                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        companies = companies.Where(w => w.Business != null && w.Business.Contains(filter.Profession));
                    }
                }

                var result = await companies.ToListAsync();
                return Ok(

                result.Select(
                w => new
                {
                    w.User.UserName,
                    w.User.Email,
                    w.User.PhoneNumber,
                    w.IsAvailable,
                    w.Location,
                    w.Business,
                    w.Bio,
                    w.ProviderType,
                }
                )

                .ToList()

                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }
        [HttpPost("contractors")]
        public async Task<IActionResult> SearchContractors([FromBody] FilterSearchDto? filter)
        {
            try
            {

                var contractors = _context.Contractors.Include(w => w.User).AsQueryable();
                if (filter != null)
                {
                    if (!string.IsNullOrEmpty(filter.FullName))
                    {
                        contractors = contractors.Where(w => w.User.UserName != null && w.User.UserName.Contains(filter.FullName));
                    }

                    if (!string.IsNullOrEmpty(filter.Location))
                    {
                        contractors = contractors.Where(w => w.Location != null && w.Location.Contains(filter.Location));
                    }

                    if (!string.IsNullOrEmpty(filter.Profession))
                    {
                        contractors = contractors.Where(w => w.Specialization != null && w.Specialization.Contains(filter.Profession));
                    }
                }
                var result = await contractors.ToListAsync();
                return Ok(

                result.Select(
                w => new
                {
                    w.User.UserName,
                    w.User.Email,
                    w.User.PhoneNumber,
                    w.IsAvailable,
                    w.Location,
                    w.Specialization,
                    w.Bio,
                    w.ProviderType,
                }
                )

                .ToList()

                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Internal Error Message {ex.Message}" });
            }
        }

    }
}