

using System.Runtime.InteropServices;
using EgyptOnline.Data;
using EgyptOnline.Dtos;
using EgyptOnline.Models;
using EgyptOnline.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgyptOnline.Controllers
{

    [Route("api/[controller]")]
    [ApiController]

    public class ProfileController : ControllerBase
    {
        private readonly UtilitiesClass _utils;
        private readonly UserManager<Worker> _userManager;

        private readonly ApplicationDbContext _context;

        public ProfileController(UserManager<Worker> userManager, UtilitiesClass utils, ApplicationDbContext context)
        {
            _utils = utils;
            _userManager = userManager;
            _context = context;
        }
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                {
                    return Unauthorized("User ID not found in token");
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found");
                }
                // How to load the skills from the Collections here
                var skills = await _context.Skills.Where(s => s.WorkerId == user.Id).ToListAsync();

                return Ok(new
                {
                    user.Id,
                    user.UserName,
                    user.Email,
                    user.Location,
                    user.IsAvailable,
                    Skills = skills.Select(s => s.Name).ToList()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }


        [HttpPut]

        public async Task<IActionResult> UpdateProfile([FromBody] UpdateWorkerProfileDto model)
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (userId == null)
                {
                    return Unauthorized("User ID not found in token");
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound("User not found");
                }

                // Update fields
                user.UserName = model.FullName ?? user.UserName;
                user.Email = model.Email ?? user.Email;
                user.Location = model.Location ?? user.Location;
                user.IsAvailable = model.IsAvailable ?? user.IsAvailable;
                if (model.Skills != null && model.Skills.Any())
                {
                    foreach (var skillName in model.Skills)
                    {
                        bool alreadyHasSkill = user.Skills.Any(s => s.Name == skillName);

                        if (!alreadyHasSkill)
                        {
                            user.Skills.Add(new Skill { Name = skillName });
                        }
                    }
                }


                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    return Ok(new { message = "Profile updated successfully!" });
                }

                return BadRequest(result.Errors);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

}