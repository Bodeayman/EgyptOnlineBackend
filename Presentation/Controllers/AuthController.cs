using EgyptOnline.Dtos;
using EgyptOnline.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using EgyptOnline.Utilities;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;
using EgyptOnline.Domain.Interfaces;
using EgyptOnline.Services;
using EgyptOnline.Data;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.AspNetCore.Identity.Data;
using System.Text.RegularExpressions;
using System.Transactions;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore.SqlServer.Migrations.Internal;
using System.Collections.Concurrent;
using EgyptOnline.Strategies;
using Microsoft.AspNetCore.Mvc.Versioning;
using EgyptOnline.Domain.Attributes;
namespace EgyptOnline.Controllers
{

    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("1.0")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly UserManager<User> _userManager;

        private readonly UserRegisterationService _userRegisterationService;
        private readonly IOTPService _smsOtpService;

        private readonly UserImageService _userImageService;

        private readonly ApplicationDbContext _context;

        private readonly ProviderRegistrationStrategyFactory _strategyFactory;



        public AuthController(UserManager<User> userManager, UserRegisterationService userRegisterationService, IUserService service, IOTPService sms, ApplicationDbContext context, UserImageService userImageService)
        {
            _userRegisterationService = userRegisterationService;
            _userService = service;
            _smsOtpService = sms;
            _context = context;
            _userImageService = userImageService;
            _userManager = userManager;
            _strategyFactory = new ProviderRegistrationStrategyFactory();
        }

        [AllowAnonymous]
        [HttpPost("register/customer")]
        public async Task<IActionResult> RegisterCustomer([FromBody] RegisterCustomerDto model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value!.Errors.Count > 0)
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                    );

                return BadRequest(new
                {
                    message = "فشل التحقق من صحة البيانات",
                    errorCode = "INVALID_INPUT",
                    errors
                });
            }

            var phoneRegex = new Regex(@"^(010|011|012|015)\d{8}$");
            if (!phoneRegex.IsMatch(model.PhoneNumber))
            {
                return BadRequest(new
                {
                    message = "رقم الهاتف يجب أن يبدأ بـ 010 أو 011 أو 012 أو 015 ويتكون من 11 رقماً",
                    errorCode = UserErrors.InvalidPhoneNumber.ToString(),
                    errors = new
                    {
                        PhoneNumber = "رقم الهاتف يجب أن يبدأ بـ 010 أو 011 أو 012 أو 015 ويتكون من 11 رقماً"
                    }
                });
            }

            var registerResult = await _userRegisterationService.RegisterCustomer(model);
            if (registerResult.Result != IdentityResult.Success)
            {
                return BadRequest(new
                {
                    message = registerResult.Result.Errors.First().Description,
                    errorCode = registerResult.Result.Errors.First().Code
                });
            }

            return Ok(new
            {
                message = "تم إنشاء حساب العميل بنجاح",
                userId = registerResult.User!.Id
            });
        }

        [AllowAnonymous]
        [HttpPost("register")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Register([FromForm] RegisterWorkerDto model, [FromForm] IFormFile? imageFile)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var phoneRegex = new Regex(@"^(010|011|012|015)\d{8}$");

                if (!phoneRegex.IsMatch(model.PhoneNumber))
                {
                    return BadRequest(new
                    {
                        message = "رقم الهاتف يجب أن يبدأ بـ 010 أو 011 أو 012 أو 015 ويتكون من 11 رقماً",
                        errorCode = UserErrors.InvalidPhoneNumber.ToString(),
                        errors = new
                        {
                            PhoneNumber = "رقم الهاتف يجب أن يبدأ بـ 010 أو 011 أو 012 أو 015 ويتكون من 11 رقماً"
                        }
                    });
                }

                var isCustomer = !string.IsNullOrWhiteSpace(model.ProviderType) &&
                                 model.ProviderType.Equals("Customer", StringComparison.OrdinalIgnoreCase);

                if (!isCustomer && imageFile == null)
                {
                    return BadRequest(new
                    {
                        message = "يرجى رفع صورة الملف الشخصي",
                        errorCode = UserErrors.ImageIsNull.ToString()
                    });
                }
                if (!ModelState.IsValid)
                {
                    // Collect all validation errors
                    var errors = ModelState
                        .Where(x => x.Value!.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                        );

                    return BadRequest(new
                    {
                        message = "فشل التحقق من صحة البيانات",
                        errorCode = UserErrors.InvalidInput.ToString(),
                        errors
                    });
                }

                if (!isCustomer && model.Pay < 100 &&
                 !model.ProviderType!.Equals("marketplace", StringComparison.CurrentCultureIgnoreCase) &&
                 !model.ProviderType.Equals("company", StringComparison.CurrentCultureIgnoreCase) &&
                 !model.ProviderType.Equals("engineer", StringComparison.CurrentCultureIgnoreCase) &&
                 !model.ProviderType.Equals("contractor", StringComparison.CurrentCultureIgnoreCase)
                 )
                {
                    return BadRequest(new
                    {
                        message = "يجب أن يكون الأجر/سعر الخدمة 100 جنيه على الأقل",
                        errorCode = UserErrors.InvalidPaymentValue.ToString()
                    });
                }
                UserRegisterationResult UserRegisterationResult = await _userRegisterationService.RegisterUser(model);
                if (UserRegisterationResult.Result != IdentityResult.Success)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, new
                    {
                        message = "حدث خطأ أثناء إنشاء الحساب",
                        errorCode = UserRegisterationResult.Result.Errors.First().Code
                    });

                }

                if (isCustomer)
                {
                    if (imageFile != null)
                    {
                        await _userImageService.UploadUserImageAsync(UserRegisterationResult.User!, imageFile);
                    }

                    await transaction.CommitAsync();
                    return Ok(new
                    {
                        message = "تم إنشاء حساب العميل بنجاح",
                        userId = UserRegisterationResult.User!.Id
                    });
                }

                if (model.ProviderType == null)
                {
                    return BadRequest(new { message = "يرجى تحديد نوع الخدمة", errorCode = UserErrors.InvalidInput.ToString() });
                }

                // Get the appropriate strategy for this provider type
                var strategy = _strategyFactory.GetStrategy(model.ProviderType);
                if (strategy == null)
                {
                    return BadRequest(new { message = "يرجى تحديد نوع الخدمة", errorCode = UserErrors.InvalidInput.ToString() });
                }

                // Validate provider-specific requirements
                var validationError = strategy.Validate(model);
                if (validationError != null)
                {
                    return BadRequest(new { message = validationError, errorCode = UserErrors.InvalidInput.ToString() });
                }

                // Create the appropriate provider using the strategy
                var provider = strategy.CreateProvider(model, UserRegisterationResult.User);
                _context.Add(provider);

                await _context.SaveChangesAsync();
                string? imageUrl = null;

                if (imageFile != null)
                {
                    imageUrl = await _userImageService.UploadUserImageAsync(UserRegisterationResult.User!, imageFile);
                    if (imageUrl == null)
                    {
                        await transaction.RollbackAsync();
                        return StatusCode(500, new
                        {
                            message = "حدث خطأ أثناء رفع صورة الملف الشخصي",
                            errorCode = UserErrors.GeneralError.ToString()
                        });
                    }
                }

                // Transaction is done here
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = $"تم إنشاء حساب مقدم الخدمة ({model.ProviderType}) بنجاح",
                    expiryDate = UserRegisterationResult.User!.Subscription!.EndDate
                });





            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = UserErrors.GeneralError.ToString() });
            }
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginWorkerDto model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.Email))
                    return BadRequest(new { message = "رقم الهاتف أو البريد الإلكتروني مطلوب", errorCode = UserErrors.InvalidInput.ToString() });

                var input = model.Email.Trim();
                User user;
                if (Helper.IsEmail(input))
                {

                    user = await _context.Users
                        .Include(u => u.Subscription)
                        .Include(u => u.ServiceProvider)
                        .FirstOrDefaultAsync(u => u.Email == input);
                    if (user == null)
                    {
                        return BadRequest(new { message = "هذا المستخدم غير موجود", errorCode = UserErrors.UserIsNotFound.ToString() });
                    }
                }
                //Egyptain Server
                else if (Helper.IsPhone(input))
                {

                    string phoneNumber = EgyptOnline.Utilities.Helper.NormalizePhoneNumber(input);
                    user = await _context.Users
                        .Include(u => u.Subscription)
                        .Include(u => u.ServiceProvider)
                        .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
                    if (user == null)
                    {
                        return BadRequest(new { message = "هذا المستخدم غير موجود", errorCode = UserErrors.UserIsNotFound.ToString() });

                    }
                }

                else
                {
                    return BadRequest(new { message = "صيغة البريد الإلكتروني أو الهاتف غير صحيحة", errorCode = UserErrors.InvalidInput.ToString() });
                }
                // Check user existence and password
                Console.WriteLine(user.Id);
                if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                    return NotFound(new { message = "البريد الإلكتروني/الهاتف أو كلمة المرور غير صحيحة", errorCode = UserErrors.EmailOrPasswordInCorrect.ToString() });
                var roles = await _userManager.GetRolesAsync(user);

                // Generate access token for all authenticated principals
                var accessToken = await _userService.GenerateJwtToken(user, TokensTypes.AccessToken);

                // If admin, return access token only (no refresh token)
                if (roles.Contains(Roles.Admin))
                {
                    return Ok(new
                    {
                        message = "تم تسجيل الدخول بنجاح",
                        accessToken,
                        role = Roles.Admin
                    });
                }

                // Regular user: determine provider role when available
                UsersTypes userRole = UsersTypes.Worker;
                if (user.ServiceProvider != null)
                {
                    if (!Enum.TryParse<UsersTypes>(user.ServiceProvider.ProviderType, out userRole))
                    {
                        return StatusCode(500, new { message = "حدث خطأ أثناء جلب دور المستخدم", errorCode = UserErrors.GeneralError.ToString() });
                    }
                }

                // Generate refresh token for regular users
                var refreshTokenString = await _userService.GenerateJwtToken(user, TokensTypes.RefreshToken);

                var refreshToken = new RefreshToken
                {
                    Token = refreshTokenString,
                    UserId = user.Id,
                    Expires = DateTime.UtcNow.AddDays(TokenPeriod.REFRESH_TOKEN_DAYS),
                    Created = DateTime.UtcNow,
                    IsRevoked = false
                };
                _context.RefreshTokens.Add(refreshToken);

                // Ensure user has a digital wallet (without a number)
                var userWallet = await _context.UserWallets.FirstOrDefaultAsync(w => w.UserId == user.Id);
                if (userWallet == null)
                {
                    _context.UserWallets.Add(new UserWallet
                    {
                        UserId = user.Id,
                        FreeBalance = 0
                    });
                }

                await _context.SaveChangesAsync();

                var responseRole = roles.Contains(Roles.Customer) ? Roles.Customer : Roles.User;

                return Ok(new
                {
                    message = "تم تسجيل الدخول بنجاح",
                    accessToken,
                    refreshToken = refreshTokenString,
                    refreshTokenExpiry = DateTime.UtcNow.AddDays(TokenPeriod.REFRESH_TOKEN_DAYS),
                    role = responseRole
                });

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = UserErrors.GeneralError.ToString() });
            }
        }

        // Admin-specific login removed. Admins are authenticated via the unified `Login` endpoint.

        [HttpPost("add-firebase-token")]
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> AddTokenToUser([FromBody] FCMDto model)
        {
            try
            {
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });

                }
                var user = await _context.Users
                    .Include(u => u.FirebaseTokens)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                Console.WriteLine("Firebase token is : " + model.fcm);

                if (user == null)
                    throw new Exception("User not found");
                Console.WriteLine("Firebase token is : " + model.fcm);
                // Check if token already exists
                if (!user.FirebaseTokens.Any(t => t.Token == model.fcm))
                {
                    user.FirebaseTokens.Add(new FirebaseToken { Token = model.fcm });
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = UserErrors.GeneralError.ToString() });
            }
            return Ok(new { message = "تم إضافة رمز Firebase بنجاح" });
        }

        // This password is for logged in user
        [HttpPost("change-password")]
        [Authorize(Roles = Roles.User)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto model)
        {
            var user = await _userManager.GetUserAsync(User); // logged-in user
            if (user == null)
            {
                return BadRequest(new { message = "المستخدم غير موجود", errorCode = UserErrors.UserIsNotFound.ToString() });
            }
            if (!await _userManager.CheckPasswordAsync(user, model.CurrentPassword))
                return BadRequest(new { message = "كلمة المرور الحالية غير صحيحة", errorCode = UserErrors.PasswordInvalid.ToString() });

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
                return BadRequest(new { message = result.Errors.First().Description, errorCode = result.Errors.First().Code });

            return Ok(new { message = "تم تغيير كلمة المرور بنجاح" });
        }
        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest refreshRequest)
        {
            if (refreshRequest == null || string.IsNullOrEmpty(refreshRequest.RefreshToken))
                return BadRequest(new { message = "رمز التحديث مطلوب", errorCode = UserErrors.InvalidInput.ToString() });

            try
            {
                // 1. Validate the token itself
                var principal = _userService.ValidateRefreshToken(refreshRequest.RefreshToken);
                if (principal == null)
                    return Unauthorized(new { message = "رمز التحديث غير صالح", errorCode = UserErrors.RefreshTokenInvalid.ToString() });

                var tokenType = principal.Claims.FirstOrDefault(c => c.Type == "token_type")?.Value;
                if (tokenType != TokensTypes.RefreshToken.ToString())
                    return Unauthorized(new { message = "الرمز المقدم ليس رمز تحديث", errorCode = UserErrors.RefreshTokenInvalid.ToString() });

                // 2. Atomic token validation + rotation with row locking
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Lock the refresh token row to prevent concurrent rotations
                    RefreshToken? storedToken = null;
                    if (_context.Database.IsRelational())
                    {
                        storedToken = await _context.RefreshTokens
                            .FromSqlInterpolated($"SELECT * FROM \"RefreshTokens\" WHERE \"Token\" = {refreshRequest.RefreshToken} FOR UPDATE")
                            .Include(rt => rt.User)
                                .ThenInclude(u => u.ServiceProvider)
                            .Include(rt => rt.User.Subscription)
                            .FirstOrDefaultAsync();
                    }
                    else
                    {
                        storedToken = await _context.RefreshTokens
                            .Include(rt => rt.User)
                                .ThenInclude(u => u.ServiceProvider)
                            .Include(rt => rt.User.Subscription)
                            .FirstOrDefaultAsync(t => t.Token == refreshRequest.RefreshToken);
                    }

                    if (storedToken == null || storedToken.Expires < DateTime.UtcNow)
                    {
                        await transaction.RollbackAsync();
                        return Unauthorized(new
                        {
                            message = "رمز التحديث غير صالح أو منتهي الصلاحية",
                            errorCode = UserErrors.RefreshTokenInvalid.ToString()
                        });
                    }

                    var user = storedToken.User;
                    if (user == null)
                    {
                        await transaction.RollbackAsync();
                        return Unauthorized(new { message = "المستخدم غير موجود", errorCode = UserErrors.UserIsNotFound.ToString() });
                    }

                    // 3. Handle Token Rotation & Grace Window for Concurrent Requests
                    if (storedToken.IsRevoked)
                    {
                        // Grace Window (60 seconds): If token was revoked in the last 60 seconds (race condition / duplicate request),
                        // return the latest active refresh token for this user instead of throwing 401.
                        if (storedToken.Revoked.HasValue && storedToken.Revoked.Value > DateTime.UtcNow.AddSeconds(-60))
                        {
                            var activeToken = await _context.RefreshTokens
                                .Where(rt => rt.UserId == user.Id && !rt.IsRevoked && rt.Expires > DateTime.UtcNow)
                                .OrderByDescending(rt => rt.Created)
                                .FirstOrDefaultAsync();

                            if (activeToken != null)
                            {
                                var graceAccessToken = await _userService.GenerateJwtToken(user, TokensTypes.AccessToken);
                                await transaction.CommitAsync();
                                return Ok(new
                                {
                                    AccessToken = graceAccessToken,
                                    RefreshToken = activeToken.Token,
                                    refreshTokenExpiry = activeToken.Expires
                                });
                            }
                        }

                        await transaction.RollbackAsync();
                        return Unauthorized(new
                        {
                            message = "رمز التحديث منتهي الصلاحية أو تم إلغاؤه",
                            errorCode = UserErrors.RefreshTokenInvalid.ToString()
                        });
                    }

                    // Revoke ONLY the presented token (multi-device isolated)
                    storedToken.IsRevoked = true;
                    storedToken.Revoked = DateTime.UtcNow;

                    // 4. Generate new tokens
                    var newAccessToken = await _userService.GenerateJwtToken(user, TokensTypes.AccessToken);
                    var newRefreshTokenString = await _userService.GenerateJwtToken(user, TokensTypes.RefreshToken);

                    var newRefreshToken = new RefreshToken
                    {
                        Token = newRefreshTokenString,
                        UserId = user.Id,
                        Expires = DateTime.UtcNow.AddDays(TokenPeriod.REFRESH_TOKEN_DAYS),
                        Created = DateTime.UtcNow,
                        IsRevoked = false
                    };

                    _context.RefreshTokens.Add(newRefreshToken);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new
                    {
                        AccessToken = newAccessToken,
                        RefreshToken = newRefreshTokenString,
                        refreshTokenExpiry = newRefreshToken.Expires,
                    });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in refresh: {ex.Message}");
                return StatusCode(500, new { message = "حدث خطأ أثناء معالجة رمز التحديث", errorCode = UserErrors.GeneralError.ToString() });
            }
        }

        [AllowAnonymous]

        [HttpPost("logout")]

        public async Task<IActionResult> Logout([FromBody] RefreshRequest refreshRequest)
        {
            if (refreshRequest == null || string.IsNullOrEmpty(refreshRequest.RefreshToken))
                return BadRequest(new { message = "رمز التحديث مطلوب", errorCode = UserErrors.InvalidInput.ToString() });

            try
            {
                // Step 1: Find and revoke the refresh token with row locking
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    RefreshToken? storedToken = null;
                    if (_context.Database.IsRelational())
                    {
                        storedToken = await _context.RefreshTokens
                            .FromSqlInterpolated($"SELECT * FROM \"RefreshTokens\" WHERE \"Token\" = {refreshRequest.RefreshToken} FOR UPDATE")
                            .FirstOrDefaultAsync();
                    }
                    else
                    {
                        storedToken = await _context.RefreshTokens
                            .FirstOrDefaultAsync(t => t.Token == refreshRequest.RefreshToken);
                    }

                    if (storedToken == null)
                    {
                        await transaction.RollbackAsync();
                        return NotFound(new { message = "رمز التحديث غير موجود", errorCode = UserErrors.RefreshTokenInvalid.ToString() });
                    }

                    storedToken.IsRevoked = true;
                    storedToken.Revoked = DateTime.UtcNow;

                    var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                    var fcmTokens = await _context.FirebaseTokens
                        .Where(t => t.user.Id == userId || t.user.Id == storedToken.UserId)
                        .ToListAsync();

                    _context.FirebaseTokens.RemoveRange(fcmTokens);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return Ok(new { message = "تم تسجيل الخروج بنجاح وإلغاء رمز التحديث" });
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in logout: {ex.Message}");
                return StatusCode(500, new { message = "حدث خطأ أثناء معالجة تسجيل الخروج", errorCode = UserErrors.GeneralError.ToString() });
            }
        }


        [Authorize(Roles = Roles.User)]
        [HttpPost("upload-profile-image")]
        // [RequireSubscription]

        public async Task<IActionResult> UploadProfileImage(IFormFile file)
        {
            try
            {
                // Get authenticated user ID
                var userId = User.Claims.FirstOrDefault(c => c.Type == "uid")?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Unauthorized(new { message = "المستخدم غير مصرح له", errorCode = "UNAUTHORIZED" });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null) return NotFound(new { message = "المستخدم غير موجود", errorCode = UserErrors.UserIsNotFound.ToString() });

                try
                {
                    var imageUrl = await _userImageService.UploadUserImageAsync(user, file);
                    return Ok(new { message = "تم رفع صورة الملف الشخصي بنجاح", imageUrl });
                }
                catch (Exception)
                {
                    return BadRequest(new { message = "فشل رفع صورة الملف الشخصي", errorCode = UserErrors.GeneralError.ToString() });
                }

            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "حدث خطأ داخلي في الخادم", errorCode = UserErrors.GeneralError.ToString() });
            }
        }
    }

    public class FCMDto
    {
        public string fcm { get; set; }
    }

}


