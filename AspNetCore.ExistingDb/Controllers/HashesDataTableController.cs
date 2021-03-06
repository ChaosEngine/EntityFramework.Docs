﻿using AspNetCore.ExistingDb.Repositories;
using EFGetStarted.AspNetCore.ExistingDb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EFGetStarted.AspNetCore.ExistingDb.Controllers
{
	public interface IHashesDataTableController : IDisposable
	{
		IActionResult Index();
		Task<IActionResult> Load(HashesDataTableLoadInput input);
	}

	public class HashesDataTableController : BaseController<ThinHashes>, IHashesDataTableController
	{
		public const string ASPX = "HashesDataTable";

		private readonly ILogger<HashesDataTableController> _logger;
		private readonly IHashesRepositoryPure _repo;
		private static readonly JsonSerializerSettings _serializationSettings =
			new JsonSerializerSettings { MetadataPropertyHandling = MetadataPropertyHandling.Ignore, Formatting = Formatting.None };

		#region Old code
		/*private IQueryable<ThinHashes> BaseItems
		{
			get
			{
				IQueryable<ThinHashes> result = _repo.GetAll()
					//.Take(2000)
					;
				return result;
			}
		}*/
		#endregion Old code

		public HashesDataTableController(IHashesRepositoryPure repo, ILogger<HashesDataTableController> logger) : base()
		{
			_logger = logger;
			_repo = repo;
			_repo.SetReadOnly(true);
		}

		[HttpGet(ASPX)]
		public virtual IActionResult Index()
		{
			#region Old code
			//String path = Request.Path;

			//var controllerName = GetType().Name.Substring(0, GetType().Name.IndexOf("controller", StringComparison.CurrentCultureIgnoreCase));
			//if (path == "/")
			//	path += controllerName;

			//if (path.IndexOf(@"/index", StringComparison.OrdinalIgnoreCase) == -1)
			//	return Redirect((path + "/Index").Replace("//", "/"));
			#endregion Old code

			string view_name = "Views/Hashes/HashesDataTable.cshtml";
			return View(view_name);
		}

		[HttpGet]
		public async Task<IActionResult> Load(HashesDataTableLoadInput input)
		{
			if (!ModelState.IsValid)
			{
#if DEBUG
				//_logger.LogWarning("!!!!!!!validation error" + Environment.NewLine +
				//	ModelState.Values.Where(m => m.ValidationState != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Valid)
				//	.SelectMany(m => m.Errors)
				//	.Select(m => m.ErrorMessage + Environment.NewLine)
				//	.Aggregate((me, me1) => me1 + " " + me));
#endif
				return BadRequest(ModelState);
			}

			CancellationToken token = HttpContext.RequestAborted;
			try
			{
				//await Task.Delay(2_000, token);

				var found = await _repo.PagedSearchAsync(input.Sort, input.Order, input.Search, input.Offset, input.Limit, token);

				var result = new
				{
					total = found.Count,
					rows = found.Itemz//.Select(x => new string[] { x.Key, x.HashMD5, x.HashSHA256 })
				};

				if (input.ExtraParam == "cached" && found.Itemz.Count() > 0)
				{
					HttpContext.Response.GetTypedHeaders().CacheControl =
						new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
						{
							Public = true,
							MaxAge = HashesRepository.HashesInfoExpirationInMinutes
						};
				}

				return Json(result, _serializationSettings);
			}
			catch (OperationCanceledException ex)
			{
				_logger.LogWarning(ex, $"!!!!!!!!!!!!!!!Cancelled {nameof(Load)}::{nameof(_repo.SearchAsync)}" +
					$"({input.Sort}, {input.Order}, {input.Search}, {input.Offset}, {input.Limit}, {token})");
				return Ok();
			}
			catch (Exception)
			{
				throw;
			}
		}
	}
}
