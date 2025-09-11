using EgyptOnline.Dtos;
using EgyptOnline.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EgyptOnline.Utilities;
using EgyptOnline.Interfaces;
namespace EgyptOnline.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserManager<Worker> _userManager;
        private readonly SignInManager<Worker> _signInManager;

        public AuthController(UserManager<Worker> userManager, SignInManager<Worker> signInManager, IUserService service)
        {
            _userService = service;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterWorkerDto model)
        {

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var user = new Worker { UserName = model.FullName, Email = model.Email, IsAvailable = true, PhoneNumber = model.PhoneNumber };
            user.IsAvailable = true;
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                return Ok(new { message = "You registered successfully!" });
            }

            return BadRequest(result.Errors);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginWorkerDto model)
        {
            try
            {

                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                {
                    return Unauthorized("Invalid login attempt");
                }

                var token = _userService.GenerateJwtToken(user);

                Console.WriteLine(token);
                return Ok(new
                {
                    message = "Login successful",
                    token = token
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error: " + ex.Message);
            }
        }



    }
}