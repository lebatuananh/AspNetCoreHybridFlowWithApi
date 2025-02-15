﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using IdentityStandaloneUserCheck.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace IdentityStandaloneUserCheck.Pages;

public class PasswordVerificationModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<PasswordVerificationModel> _logger;

    public PasswordVerificationModel(SignInManager<ApplicationUser> signInManager,
        ILogger<PasswordVerificationModel> logger,
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    [BindProperty]
    public CheckModel Input { get; set; }

    public IList<AuthenticationScheme> ExternalLogins { get; set; }

    public string ReturnUrl { get; set; }

    [TempData]
    public string ErrorMessage { get; set; }

    public class CheckModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        var hasPassword = await _userManager.HasPasswordAsync(user);
        if (!hasPassword)
        {
            return NotFound($"User has no password'{_userManager.GetUserId(User)}'.");
        }

        returnUrl ??= Url.Content("~/");
        ReturnUrl = returnUrl;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
        }

        if (ModelState.IsValid)
        {
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            var result = await _signInManager.PasswordSignInAsync(user.Email, Input.Password, false, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                _logger.LogInformation("User password re-entered");

                await RemovePasswordCheck(user);
                var claim = new Claim(PasswordVerificationBase.PasswordCheckedClaimType,
                    DateTime.UtcNow.ToFileTimeUtc().ToString());
                await _userManager.AddClaimAsync(user, claim);
                await _signInManager.RefreshSignInAsync(user);

                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }

    private async Task RemovePasswordCheck(ApplicationUser user)
    {
        if (User.HasClaim(c => c.Type == PasswordVerificationBase.PasswordCheckedClaimType))
        {
            var claims = User.FindAll(PasswordVerificationBase.PasswordCheckedClaimType);
            foreach (Claim c in claims)
            {
                await _userManager.RemoveClaimAsync(user, c);
            }
        }
    }
}