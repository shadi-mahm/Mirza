﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Mirza.Web.Dto;
using Mirza.Web.Models;
using Mirza.Web.Services.User;

namespace Mirza.Web.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly IUserService _userService;
        private readonly UserManager<MirzaUser> _userManager;

        public IndexModel(IUserService userService, UserManager<MirzaUser> userManager)
        {
            _userService = userService;
            _userManager = userManager;
        }
        [BindProperty]
        public InputModel Input { get; set; }
        public WorkLogReportOutput TodayWorkLog { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Display(Name = "تاریخ")]
            [DataType(DataType.Date)]
            [Required(AllowEmptyStrings = false, ErrorMessage = "کاری که تاریخ نداره رو من تو کدوم صفحه‌ی تقویم بنویسم؟")]
            public DateTime Date { get; set; } = DateTime.Now;

            [Display(Name = "ساعت شروع")]
            [DataType(DataType.Time)]
            [Required(AllowEmptyStrings = false, ErrorMessage = "کاری که ساعت شروع نداشته باشه که نمی‌شه. نه؟")]
            public TimeSpan Start { get; set; }

            [Display(Name = "ساعت پایان")]
            [DataType(DataType.Time)]
            [Required(AllowEmptyStrings = false, ErrorMessage = "کاری که تموم نشده رو چطوری ثبت می‌کنی؟")]
            public TimeSpan End { get; set; }

            [Display(Name = "توضیح")]
            [DataType(DataType.MultilineText)]
            [StringLength(500, ErrorMessage = "چه خبرته؟ توضیحات رو به ۵۰۰ کاراکتر محدود کن!")]
            public string Description { get; set; }

            [Display(Name = "جزئیات")]
            [DataType(DataType.MultilineText)]
            [StringLength(500, ErrorMessage = "چه خبرته؟ جزئیات رو به ۵۰۰ کاراکتر محدود کن!")]
            public string Details { get; set; }

        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User).ConfigureAwait(false);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            TodayWorkLog = await _userService.GetWorkLogReport(user.Id, DateTime.Now.Date).ConfigureAwait(false);

            Input = new InputModel();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User).ConfigureAwait(false);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            try
            {
                _ = await _userService.AddWorkLog(user.Id, new WorkLog
                {
                    User = user,
                    UserId = user.Id,
                    Description = Input.Description ?? "-",
                    Details = Input.Details ?? "-",
                    EntryDate = Input.Date,
                    StartTime = Input.Start,
                    EndTime = Input.End
                }).ConfigureAwait(false);
            }
            catch (ArgumentNullException)
            {
                ErrorMessage = "خالیه! چی رو بنویسم؟";
            }
            catch (WorkLogModelValidationException)
            {
                ErrorMessage = "من این زبون رو نفهمیدم. لطفن ورودی‌هات رو دوباره چک کن.";
            }
            catch (ArgumentException)
            {
                ErrorMessage = "بله؟ شما؟";
            }
            catch (InvalidOperationException)
            {
                ErrorMessage = "مگه میشه تو یه بازه‌ی زمانی دو تا کار کرده باشی؟ هان؟‌ میشه؟";
            }
            catch (Exception)
            {
                ErrorMessage = "یه اتفاقی افتاده عجیب! نمی‌دونم چی‌کار کنم!";
            }

            return RedirectToPage();
        }
    }
}