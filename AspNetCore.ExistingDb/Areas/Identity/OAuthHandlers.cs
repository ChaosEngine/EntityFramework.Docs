﻿using IdentitySample.DefaultUI.Data;
using InkBall.Module;
using InkBall.Module.Hubs;
using InkBall.Module.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.Twitter;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace AspNetCore.ExistingDb.Helpers
{
	public class MyGoogleHandler : GoogleHandler
	{
		private readonly IConfiguration _configuration;

		public MyGoogleHandler(IOptionsMonitor<GoogleOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, IConfiguration configuration)
			: base(options, logger, encoder, clock)
		{
			_configuration = configuration;
		}

		public override Task<bool> ShouldHandleRequestAsync()
		{
			return Task.FromResult(Options.CallbackPath.Value.Replace(_configuration["AppRootPath"], "/") == Request.Path.Value);
		}
	}

	public class MyTwitterHandler : TwitterHandler
	{
		private readonly IConfiguration _configuration;

		public MyTwitterHandler(IOptionsMonitor<TwitterOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, IConfiguration configuration)
		   : base(options, logger, encoder, clock)
		{
			_configuration = configuration;
		}

		public override Task<bool> ShouldHandleRequestAsync()
		{
			return Task.FromResult(Options.CallbackPath.Value.Replace(_configuration["AppRootPath"], "/") == Request.Path.Value);
		}
	}

	public class MyGithubHandler : OAuthHandler<MyGithubHandler.GitHubOptions>
	{
		private readonly IConfiguration _configuration;

		public class GitHubOptions : OAuthOptions
		{
			public GitHubOptions()
			{
				AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
				TokenEndpoint = "https://github.com/login/oauth/access_token";
				UserInformationEndpoint = "https://api.github.com/user";
				ClaimsIssuer = "OAuth2-Github";
				SaveTokens = true;
				// Retrieving user information is unique to each provider.
				ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
				ClaimActions.MapJsonKey(ClaimTypes.Name, "login");
				ClaimActions.MapJsonKey("urn:github:name", "name");
				ClaimActions.MapJsonKey(ClaimTypes.Email, "email", ClaimValueTypes.Email);
				ClaimActions.MapJsonKey("urn:github:url", "url");
			}
		}

		public MyGithubHandler(IOptionsMonitor<GitHubOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, IConfiguration configuration)
		   : base(options, logger, encoder, clock)
		{
			_configuration = configuration;
		}

		public override Task<bool> ShouldHandleRequestAsync()
		{
			return Task.FromResult(Options.CallbackPath.Value.Replace(_configuration["AppRootPath"], "/") == Request.Path.Value);
		}

		protected async override Task<AuthenticationTicket> CreateTicketAsync(ClaimsIdentity identity, AuthenticationProperties properties, OAuthTokenResponse tokens)
		{
			using (var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInformationEndpoint))
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

				using (var response = await Backchannel.SendAsync(request, Context.RequestAborted))
				{
					if (!response.IsSuccessStatusCode)
					{
						throw new HttpRequestException($"An error occurred when retrieving Google user information ({response.StatusCode}). Please check if the authentication information is correct and the corresponding Google+ API is enabled.");
					}

					var payload = JObject.Parse(await response.Content.ReadAsStringAsync());

					var context = new OAuthCreatingTicketContext(new ClaimsPrincipal(identity), properties, Context, Scheme, Options, Backchannel, tokens, payload);
					context.RunClaimActions();

					await Events.CreatingTicket(context);
					return new AuthenticationTicket(context.Principal, context.Properties, Scheme.Name);
				}
			}
		}
	}

	public class MySignInManager : SignInManager<ApplicationUser>
	{
		private readonly GamesContext _inkBallContext;
		private readonly IHubContext<ChatHub, IChatClient> _inkballHubContext;

		public MySignInManager(
			UserManager<ApplicationUser> userManager,
			IHttpContextAccessor contextAccessor,
			IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
			IOptions<IdentityOptions> optionsAccessor,
			ILogger<SignInManager<ApplicationUser>> logger,
			IAuthenticationSchemeProvider schemes,
			GamesContext inkBallContext,
			IHubContext<InkBall.Module.Hubs.ChatHub, InkBall.Module.Hubs.IChatClient> inkballHubContext
			) : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes)
		{
			_inkBallContext = inkBallContext;
			_inkballHubContext = inkballHubContext;
		}

		public override async Task<ClaimsPrincipal> CreateUserPrincipalAsync(ApplicationUser user)
		{
			var principal = await base.CreateUserPrincipalAsync(user);

			// use this.UserManager if needed
			var identity = (ClaimsIdentity)principal.Identity;
			var name_identifer = principal.FindFirstValue(ClaimTypes.NameIdentifier);

			if (!string.IsNullOrEmpty(name_identifer) && user.Age >= MinimumAgeRequirement.MinimumAge)//conditions for InkBallUser to create
			{
				InkBallUser found_user = _inkBallContext.InkBallUsers.FirstOrDefault(i => i.sExternalId == name_identifer);
				if (found_user != null)
				{
					//user already created and existing in InkBallUsers, awesome.
					//just update user name if differs
					if (found_user.UserName != user.Name)
					{
						found_user.UserName = user.Name;
						await _inkBallContext.SaveChangesAsync(Context.RequestAborted);
					}
				}
				else
				{
					found_user = new InkBallUser
					{
						sExternalId = name_identifer,
						iPrivileges = 0,
						UserName = user.Name
					};
					await _inkBallContext.InkBallUsers.AddAsync(found_user, Context.RequestAborted);
					await _inkBallContext.SaveChangesAsync(Context.RequestAborted);
				}

				if (!identity.HasClaim(x => x.Type == nameof(InkBall.Module.Pages.HomeModel.InkBallUserId)))
				{
					identity.AddClaim(new Claim(nameof(InkBall.Module.Pages.HomeModel.InkBallUserId), found_user.iId.ToString(),
						nameof(InkBall.Module.Model.InkBallUser)));
				}

				if (!identity.HasClaim(x => x.Type == ClaimTypes.DateOfBirth))
				{
					var date_of_birth = new Claim(ClaimTypes.DateOfBirth,
						DateTime.UtcNow.AddYears(-user.Age).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
					identity.AddClaim(date_of_birth);
				}
			}

			return principal;
		}

		public override async Task SignOutAsync()
		{
			var token = base.Context.RequestAborted;
			var name_identifer = base.Context.User.FindFirstValue(ClaimTypes.NameIdentifier);

			var games_to_surrender = await _inkBallContext.InkBallGame
				.Include(gp1 => gp1.Player1).ThenInclude(p1 => p1.User)
				.Include(gp2 => gp2.Player2).ThenInclude(p2 => p2.User)
				.Where(w =>
				   (w.Player1.User.sExternalId == name_identifer || w.Player2.User.sExternalId == name_identifer)
				   && (w.GameState == InkBallGame.GameStateEnum.ACTIVE || w.GameState == InkBallGame.GameStateEnum.AWAITING)
				).ToArrayAsync(token);

			if (games_to_surrender.Any())
			{
				using (var trans = await _inkBallContext.Database.BeginTransactionAsync(token))
				{
					try
					{
						foreach (InkBallGame game in games_to_surrender)
						{
							await _inkBallContext.SurrenderGameFromPlayerAsync(game, base.Context.Session, false, token);

							if (_inkballHubContext != null)
							{
								InkBallPlayer player_not_signed_off, player_signed_off;
								if (game.GetPlayer()?.User?.sExternalId == name_identifer)
								{
									player_signed_off = game.GetPlayer();
									player_not_signed_off = game.GetOtherPlayer();
								}
								else
								{
									player_signed_off = game.GetOtherPlayer();
									player_not_signed_off = game.GetPlayer();
								}

								var tsk = Task.Factory.StartNew(async (payload) =>
								{
									try
									{
										var signedOff_id_online = payload as Tuple<string, int?, string>;

										if (!string.IsNullOrEmpty(signedOff_id_online.Item1))
										{
											await _inkballHubContext.Clients.User(signedOff_id_online.Item1).ServerToClientPlayerSurrender(
												new PlayerSurrenderingCommand(signedOff_id_online.Item2.GetValueOrDefault(0), true,
												$"Player {signedOff_id_online.Item3 ?? ""} logged out"));
										}
									}
									catch (Exception ex)
									{
										Logger.LogError(ex.Message);
									}
								},
								Tuple.Create(player_not_signed_off?.User?.sExternalId, player_signed_off?.iId, player_signed_off?.User?.UserName),
								token);
							}
						}

						trans.Commit();
					}
					catch (Exception ex)
					{
						trans.Rollback();
						Logger.LogError(ex, ex.Message);
					}
				}
			}

			await base.SignOutAsync();
		}
	}
}
