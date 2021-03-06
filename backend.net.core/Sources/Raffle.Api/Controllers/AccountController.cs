﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Raffle.Api.Helpers;
using Raffle.Api.Models;
using Raffle.Api.ViewModels;
using Raffle.Dal;
using Raffle.Domain.Interface.Entity;
using Raffle.Domain.Interface.Services;
using Raffle.Infrastructure.Interface;

namespace Raffle.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly ApplicationDbContext _appDbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMapper _mapper;
        private readonly IEmailSender _emailSender;
        private readonly IEmailBuilder _emailBuilder;
        private readonly ICustomerService _customerService;
        private readonly IMessageModelBuilder _messageModelBuilder;
        private readonly IConfiguration _config;


        public AccountController(
            UserManager<ApplicationUser> userManager,
            IMapper mapper,
            ApplicationDbContext appDbContext,
            IEmailSender emailSender,
            IEmailBuilder emailBuilder,
            ICustomerService customerService,
            IMessageModelBuilder messageModelBuilder,
            IConfiguration config
            )
        {
            _userManager = userManager;
            _mapper = mapper;
            _appDbContext = appDbContext;
            _emailSender = emailSender;
            _emailBuilder = emailBuilder;
            _customerService = customerService;
            _messageModelBuilder = messageModelBuilder;
            _config = config;
        }

        [HttpPost, Route("register")]
        public async Task<IActionResult> Register([FromBody]RegistrationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userIdentity = _mapper.Map<ApplicationUser>(model);
                var result = await _userManager.CreateAsync(userIdentity, model.Password);
                if (!result.Succeeded) return new BadRequestObjectResult(Errors.AddErrorsToModelState(result, ModelState));
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(userIdentity);
                var callbackUrl = Url.Action($"ConfirmEmail", $"Account", new { userId = userIdentity.Id, code = code }, protocol: HttpContext.Request.Scheme);
                await _emailSender.SendEmailAsync(model.Email, "Confirm your account",
                    _emailBuilder.CreateConfirmEmailBody(callbackUrl));
                //await _signInManager.SignInAsync(user, isPersistent: false);
                //await _customerService.CreateAsync(new Customer {IdentityId = userIdentity.Id});
                //await _appDbContext.Customers.AddAsync(new Customer { IdentityId = userIdentity.Id});
                await _userManager.AddToRolesAsync(userIdentity, new List<string> { Constants.StandartRole, Constants.Admin });
                //await _appDbContext.SaveChangesAsync();
                return new OkObjectResult("Account created");
            }
            catch (Exception e)
            {
                return BadRequest(new { e.Message });
            }
        }

        [HttpGet, Route("ConfirmEmail")]
        public IActionResult ConfirmEmail(string userId, string code)
        {
            try
            {
                var user = _userManager.FindByIdAsync(userId).Result;
                if (user == null) return BadRequest("Не удалось определить пользователя");
                var result = _userManager.ConfirmEmailAsync(user, code).Result;
                if (!result.Succeeded) return BadRequest("Не удалось определить токен доступа");
                return Ok("Ваш E-mail успешно подтверждён. Теперь вы можете авторизоваться");
            }
            catch (Exception e)
            {
                return BadRequest(e.Message);
            }
        }

        [HttpPost, Route("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                var user = await _userManager.FindByNameAsync(model.Email);
                if (user == null) return BadRequest(_messageModelBuilder.CreateModel("message", "Указанный пользователь не найден"));
                if (!await _userManager.IsEmailConfirmedAsync(user)) return BadRequest(_messageModelBuilder.CreateModel("message", "E-mail не подтвержден"));
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                var webSiteUrl = _config.GetSection("WebSite").Value;
                var callbackUrl = string.Format(webSiteUrl + "?id={0}&code={1}", user.Id, code);
                //var callbackUrl = Url.Action($"PasswordReset", $"Account", new {userId = user.Id, code = code}, HttpContext.Request.Scheme); 
                await _emailSender.SendEmailAsync(model.Email, "Reset your password",
                    _emailBuilder.CreateForgotPasswordEmailBody(callbackUrl));
                return Ok("На Ваш E-mail отправлено письмо для восстановления пароля.");
            }
            catch (Exception e)
            {
                return BadRequest(_messageModelBuilder.CreateModel("500", e.Message));
            }
        }

        [HttpPost, Route("PasswordReset")]
        public async Task<IActionResult> PasswordReset([FromBody] ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                model.Code = model.Code.Replace(" ", "+");
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user == null) return BadRequest(_messageModelBuilder.CreateModel("message", "Указанный пользователь не найден"));
                var resultVerifyToken = await _userManager.VerifyUserTokenAsync(user, "Default", "ResetPassword", model.Code);
                if (resultVerifyToken)
                {
                    var result = _userManager.ResetPasswordAsync(user, model.Code, model.Password).Result;
                    if (!result.Succeeded) return BadRequest(_messageModelBuilder.CreateModel("message", "Ошибка при сбросе пароля. Возможно ссылка не действительна"));

                    return Ok();
                }
                return BadRequest(_messageModelBuilder.CreateModel("message", "Ссылка для восттановления пароля не действительна!"));
            }
            catch (Exception e)
            {
                return BadRequest(_messageModelBuilder.CreateModel("500", e.Message));
            }
        }

    }
}