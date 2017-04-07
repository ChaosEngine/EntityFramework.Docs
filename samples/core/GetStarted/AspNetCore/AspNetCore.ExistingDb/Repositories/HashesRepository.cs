﻿using EFGetStarted.AspNetCore.ExistingDb;
using EFGetStarted.AspNetCore.ExistingDb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCore.ExistingDb.Repositories
{
	public interface IHashesRepository : IGenericRepository<BloggingContext, ThinHashes>
	{
		Task<HashesInfo> CurrentHashesInfo { get; }

		Task<List<ThinHashes>> AutoComplete(string text);

		HashesInfo CalculateHashesInfo<T>(ILoggerFactory _loggerFactory, ILogger<T> _logger, IConfiguration conf) where T : Controller;
	}

	public class HashesRepository : GenericRepository<BloggingContext, ThinHashes>, IHashesRepository
	{
		/// <summary>
		/// Used value or this specific worker node/process or load balancing server
		/// </summary>
		private static HashesInfo _hashesInfoStatic;
		/// <summary>
		/// locally cached value for request, refreshed upon every request.
		/// </summary>
		private HashesInfo _hi;
		private static readonly object _locker = new object();

		public Task<HashesInfo> CurrentHashesInfo
		{
			get { return GetHashesInfoFromDB(_entities); }
		}

		private async Task<HashesInfo> GetHashesInfoFromDB(BloggingContext db)
		{
			if (_hashesInfoStatic == null)
			{
				if (_hi == null)            //local value is empty, fill it from DB once
					_hi = await db.HashesInfo.FirstOrDefaultAsync(x => x.ID == 0);

				if (_hi == null || _hi.IsCalculating)
					return _hi;             //still calculating, return just this local value
				else
					_hashesInfoStatic = _hi;//calculation ended, save to global static value
			}
			return _hashesInfoStatic;
		}

		public HashesRepository(BloggingContext context) : base(context)
		{
		}

		public Task<List<ThinHashes>> AutoComplete(string text)
		{
			text = text.Trim().ToLower();
			Task<List<ThinHashes>> found = null;

			switch (BloggingContext.ConnectionTypeName)
			{
				case "sqliteconnection":
				case "mysqlconnection":
					found = (from x in _entities.ThinHashes
							 where (x.HashMD5.StartsWith(text) || x.HashSHA256.StartsWith(text))
							 select x)
						.Take(20)
						.DefaultIfEmpty(new ThinHashes { Key = "nothing found" })
						.ToListAsync();
					break;

				case "sqlconnection":
					found = _entities.ThinHashes.FromSql(
$@"SELECT TOP 20 * FROM (
	SELECT x.[{nameof(Hashes.Key)}], x.{nameof(Hashes.HashMD5)}, x.{nameof(Hashes.HashSHA256)}
	FROM {nameof(Hashes)} AS x
	WHERE x.{nameof(Hashes.HashMD5)} like cast(@text as varchar)
	UNION ALL
	SELECT y.[{nameof(Hashes.Key)}], y.{nameof(Hashes.HashMD5)}, y.{nameof(Hashes.HashSHA256)}
	FROM {nameof(Hashes)} AS y
	WHERE y.{nameof(Hashes.HashSHA256)} like cast(@text as varchar)
) a", new SqlParameter("@text", text + '%'))
						.DefaultIfEmpty(new ThinHashes { Key = "nothing found" })
						.ToListAsync();
					break;

				default:
					throw new NotSupportedException($"Bad {nameof(BloggingContext.ConnectionTypeName)} name");
			}

			return found;
		}

		/// <summary>
		/// Sync running method
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="loggerFactory"></param>
		/// <param name="logger"></param>
		/// <param name="configuration"></param>
		/// <returns></returns>
		public HashesInfo CalculateHashesInfo<T>(ILoggerFactory loggerFactory, ILogger<T> logger, IConfiguration configuration) where T : Controller
		{
			HashesInfo hi = null;

			var bc = new DbContextOptionsBuilder<BloggingContext>();
			bc.UseLoggerFactory(loggerFactory);
			Startup.ConfigureDBKind(bc, configuration);

			using (var db = new BloggingContext(bc.Options))
			{
				db.Database.SetCommandTimeout(180);//long long running. timeouts prevention
				//in sqlite only serializable - https://sqlite.org/isolation.html
				var isolation_level = BloggingContext.ConnectionTypeName == "sqliteconnection" ? IsolationLevel.Serializable: IsolationLevel.ReadUncommitted;
				using (var trans = db.Database.BeginTransaction(isolation_level))//needed, other web nodes will read saved-caculating-state and exit thread
				{
					try
					{
						if (GetHashesInfoFromDB(db).Result != null)
						{
							logger.LogInformation(0, $"###Leaving calculation of initial Hash parameters; already present");
							return GetHashesInfoFromDB(db).Result;
						}
						logger.LogInformation(0, $"###Starting calculation of initial Hash parameters");

						hi = new HashesInfo { ID = 0, IsCalculating = true };

						db.HashesInfo.Add(hi);
						db.SaveChanges(true);
						_hashesInfoStatic = hi;//temporary save to static to indicate calculation and block new calcultion threads

						var alphabet = (from h in db.ThinHashes
										select h.Key.First()
										).Distinct()
										.OrderBy(o => o);
						var count = db.ThinHashes.Count();
						var key_length = db.ThinHashes.Max(x => x.Key.Length);

						hi.Count = count;
						hi.KeyLength = key_length;
						hi.Alphabet = string.Concat(alphabet);
						hi.IsCalculating = false;

						db.Update(hi);
						db.SaveChanges(true);

						trans.Commit();
						logger.LogInformation(0, $"###Calculation of initial Hash parameters ended");
					}
					catch (Exception)
					{
						trans.Rollback();
						hi = null;
					}
					finally
					{
						_hashesInfoStatic = hi;
					}
					return hi;
				}
			}
		}
	}
}
