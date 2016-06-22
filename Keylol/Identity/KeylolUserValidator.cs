using System;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Keylol.Models;
using Keylol.ServiceBase;
using Keylol.Utilities;
using Microsoft.AspNet.Identity;
using Constants = Keylol.Utilities.Constants;

namespace Keylol.Identity
{
    /// <summary>
    ///     ASP.NET Identity UserValidator Keylol implementation
    /// </summary>
    public class KeylolUserValidator : IIdentityValidator<KeylolUser>
    {
        private readonly KeylolUserManager _userManager;

        /// <summary>
        ///     ���� <see cref="KeylolUserValidator" />
        /// </summary>
        /// <param name="userManager">
        ///     <see cref="KeylolUserManager" />
        /// </param>
        public KeylolUserValidator(KeylolUserManager userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        ///     Validates a user before saving
        /// </summary>
        /// <param name="user" />
        /// <returns />
        public async Task<IdentityResult> ValidateAsync(KeylolUser user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));

            // IdCode

            var idCodeOwner = await _userManager.FindByIdCodeAsync(user.IdCode);
            if (idCodeOwner == null)
            {
                if (!Regex.IsMatch(user.IdCode, Constants.IdCodeConstraint))
                    return IdentityResult.Failed(Errors.InvalidIdCode);

                if (IsIdCodeReserved(user.IdCode))
                    return IdentityResult.Failed(Errors.IdCodeReserved);
            }
            else if (idCodeOwner.Id != user.Id)
            {
                return IdentityResult.Failed(Errors.IdCodeUsed);
            }

            // UserName

            if (user.UserName.Length < 3 || user.UserName.Length > 16)
                return IdentityResult.Failed(Errors.UserNameInvalidLength);

            if (!Regex.IsMatch(user.UserName, Constants.UserNameConstraint))
                return IdentityResult.Failed(Errors.UserNameInvalidCharacter);

            var userNameOwner = await _userManager.FindByNameAsync(user.UserName);
            if (userNameOwner != null && userNameOwner.Id != user.Id)
                return IdentityResult.Failed(Errors.UserNameUsed);

            // Email

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                user.Email = null;
            }
            else
            {
                if (!new EmailAddressAttribute().IsValid(user.Email))
                    return IdentityResult.Failed(Errors.InvalidEmail);

                var emailOwner = await _userManager.FindByEmailAsync(user.Email);
                if (emailOwner != null && emailOwner.Id != user.Id)
                    return IdentityResult.Failed(Errors.EmailUsed);
            }

            // GamerTag

            if (user.GamerTag.Length > 40)
                return IdentityResult.Failed(Errors.GamerTagInvalidLength);

            // AvatarImage

            if (string.IsNullOrWhiteSpace(user.AvatarImage))
                user.AvatarImage = string.Empty;
            else if (!Helpers.IsTrustedUrl(user.AvatarImage))
                return IdentityResult.Failed(Errors.AvatarImageUntrusted);

            // HeaderImage

            if (string.IsNullOrWhiteSpace(user.HeaderImage))
                user.HeaderImage = string.Empty;
            else if (!Helpers.IsTrustedUrl(user.HeaderImage))
                return IdentityResult.Failed(Errors.HeaderImageUntrusted);

            // ThemeColor

            try
            {
                user.ThemeColor = string.IsNullOrWhiteSpace(user.ThemeColor)
                    ? string.Empty
                    : ColorTranslator.ToHtml(ColorTranslator.FromHtml(user.ThemeColor));
            }
            catch (Exception)
            {
                return IdentityResult.Failed(Errors.InvalidThemeColor);
            }

            // LightThemeColor

            try
            {
                user.LightThemeColor = string.IsNullOrWhiteSpace(user.LightThemeColor)
                    ? string.Empty
                    : ColorTranslator.ToHtml(ColorTranslator.FromHtml(user.LightThemeColor));
            }
            catch (Exception)
            {
                return IdentityResult.Failed(Errors.InvalidLightThemeColor);
            }

            return IdentityResult.Success;
        }

        /// <summary>
        /// �ж�ָ��ʶ�����Ƿ񱻱���
        /// </summary>
        /// <param name="idCode">ʶ����</param>
        /// <returns>���ʶ���뱻���������� <c>true</c></returns>
        public static bool IsIdCodeReserved(string idCode)
        {
            if (new[]
            {
                @"^([A-Z0-9])\1{4}$",
                @"^0000\d$",
                @"^\d0000$",
                @"^TEST.$",
                @"^.TEST$"
            }.Any(pattern => Regex.IsMatch(idCode, pattern)))
                return true;

            if (new[]
            {
                "12345",
                "54321",
                "ADMIN",
                "STAFF",
                "KEYLO",
                "KYLOL",
                "KEYLL",
                "VALVE",
                "STEAM",
                "CHINA",
                "JAPAN"
            }.Contains(idCode))
                return true;

            return false;
        }
    }
}