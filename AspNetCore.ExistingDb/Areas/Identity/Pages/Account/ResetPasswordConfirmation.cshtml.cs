// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentitySample.DefaultUI

{
    [AllowAnonymous]
    public class ResetPasswordConfirmationModel : PageModel
    {
		public static readonly string ASPX = "~/Identity/Account/ResetPasswordConfirmation";

		public void OnGet()
        {
        }
    }
}
