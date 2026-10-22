using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudentFootballLeague.BusinessLogic.DTOs;
using StudentFootballLeague.WebClient.Services;

namespace StudentFootballLeague.WebClient.Controllers
{
    public class AuthController : Controller
    {
        private readonly IApiClientService _apiClient;

        public AuthController(IApiClientService apiClient)
        {
            _apiClient = apiClient;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginRequestDto dto, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            var result = await _apiClient.PostAsync<LoginRequestDto, AuthResponseDto>("api/auth/login", dto);
            if (!result.IsSuccess || result.Data == null)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Email hoặc mật khẩu không chính xác.");
                return View(dto);
            }

            // Giải mã JWT để lấy Claims
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(result.Data.AccessToken);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, result.Data.UserId.ToString()),
                new Claim(ClaimTypes.Name, result.Data.FullName),
                new Claim(ClaimTypes.Email, result.Data.Email),
                new Claim(ClaimTypes.Role, result.Data.Role),
                new Claim("AccessToken", result.Data.AccessToken)
            };

            if (result.Data.ManagedTeamId.HasValue)
            {
                claims.Add(new Claim("TeamId", result.Data.ManagedTeamId.Value.ToString()));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(180)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Lưu JWT token vào Session làm backup
            HttpContext.Session.SetString("JWToken", result.Data.AccessToken);

            TempData["Success"] = $"Đăng nhập thành công! Chào mừng {result.Data.FullName}.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (result.Data.Role == "Admin")
            {
                return RedirectToAction("Tournaments", "Admin");
            }
            if (result.Data.Role == "TeamManager")
            {
                return RedirectToAction("Dashboard", "MyTeam");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return View(dto);
            }

            dto.Role = "TeamManager";
            var result = await _apiClient.PostAsync<RegisterRequestDto, AuthResponseDto>("api/auth/register", dto);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Đăng ký không thành công.");
                return View(dto);
            }

            TempData["Success"] = "Đăng ký tài khoản Đội trưởng thành công! Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            TempData["Success"] = "Đã đăng xuất khỏi hệ thống.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
