﻿using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mirza.Web.Data;
using Mirza.Web.Models;
using Mirza.Web.Validators;

namespace Mirza.Web.Services.User
{
    public class UserService : IUserService
    {
        private readonly MirzaDbContext _dbContext;
        private readonly ILogger<UserService> _logger;
        private readonly UserValidator _userValidator;
        private readonly WorkLogValidator _workLogValidator;

        public UserService(MirzaDbContext dbContext, ILogger<UserService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _userValidator = new UserValidator();
            _workLogValidator = new WorkLogValidator();
        }

        #region AccessKey Manipulation

        public async Task<AccessKey> AddAccessKey(int userId)
        {
            var user = await _dbContext.UserSet.Include(u => u.AccessKeys)
                                       .SingleOrDefaultAsync(s => s.Id == userId)
                                       .ConfigureAwait(false);

            if (user == null)
            {
                throw new AccessKeyException($"User id {userId} does not exist");
            }

            if (!user.IsActive)
            {
                throw new AccessKeyException($"User id {userId} is not active");
            }

            try
            {
                var accessKey = new AccessKey(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture))
                {
                    OwnerId = userId
                };
                user.AccessKeys.Add(accessKey);
                await _dbContext.SaveChangesAsync().ConfigureAwait(false);
                return accessKey;
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception occured while adding new access key for user {userId}", e);
                throw;
            }
        }

        public async Task<AccessKey> DeactivateAccessKey(int userId, string accessKey)
        {
            var user = await _dbContext.UserSet.Include(u => u.AccessKeys)
                                       .SingleOrDefaultAsync(s => s.Id == userId)
                                       .ConfigureAwait(false);

            if (user == null)
            {
                throw new AccessKeyException($"User id {userId} does not exist");
            }

            if (!user.IsActive)
            {
                throw new AccessKeyException($"User id {userId} is not active");
            }

            if (!user.AccessKeys.Any(a => a.Key == accessKey))
            {
                throw new AccessKeyException("Invalid access key");
            }

            var found = user.AccessKeys.Single(a =>
                string.Equals(accessKey, a.Key, StringComparison.OrdinalIgnoreCase));
            if (!found.IsActive)
            {
                // Do noting here, the access key is already not active due to
                // expiration or state.
                return found;
            }

            found.State = AccessKeyState.Inative;
            try
            {
                await _dbContext.SaveChangesAsync().ConfigureAwait(false);
                return found;
            }
            catch (Exception e)
            {
                _logger.LogError($"Exception occured while deactivating accesskey: {found.Id}", e);
                throw;
            }
        }

        public async Task<MirzaUser> GetUserWithActiveAccessKey(string accessKey)
        {
            try
            {
                var foundUser = await _dbContext.UserSet
                                                .Include(a => a.AccessKeys)
                                                .Include(t => t.Team)
                                                .SingleOrDefaultAsync(
                                                    user => user.IsActive &&
                                                            user.AccessKeys.Any(
                                                                ak => ak.State == AccessKeyState.Active &&
                                                                      ak.Expiration >= DateTime.UtcNow &&
                                                                      accessKey == ak.Key)
                                                )
                                                .ConfigureAwait(false);
                return foundUser;
            }
            catch (Exception e)
            {
                _logger.LogError("Exception occured while querying for user with access key", e);
                return null;
            }
        }

        #endregion

        #region WorkLog Manipulation

        public async Task<WorkLog> AddWorkLog(int userId, WorkLog workLog)
        {
            if (workLog == null)
            {
                throw new ArgumentNullException(nameof(workLog));
            }

            var validationResult = _workLogValidator.Validate(workLog);
            if (!validationResult.IsValid)
            {
                throw new WorkLogModelValidationException(validationResult
                                                          .Errors.Select(e => e.ErrorMessage).ToArray());
            }

            var user = await _dbContext.UserSet
                                       .Include(u => u.WorkLog)
                                       .SingleOrDefaultAsync(s => s.IsActive && s.Id == userId)
                                       .ConfigureAwait(false);

            if (user == null)
            {
                throw new ArgumentException("Invalid userId", nameof(userId));
            }

            var overlappingLogExists = user.WorkLog.Any(w =>
                w.EntryDate.Date == workLog.EntryDate.Date &&
                (workLog.StartTime >= w.StartTime && workLog.StartTime < w.EndTime
                 || workLog.EndTime > w.StartTime && workLog.StartTime < w.StartTime));

            if (overlappingLogExists)
            {
                throw new InvalidOperationException("Can not log overlapping work periods for a given date");
            }

            try
            {
                var addResult = await _dbContext.WorkLogSet.AddAsync(new WorkLog
                {
                    Description = workLog.Description ?? "-",
                    Details = workLog.Details ?? "-",
                    EntryDate = workLog.EntryDate.Date,
                    StartTime = workLog.StartTime,
                    EndTime = workLog.EndTime,
                    UserId = user.Id
                });
                await _dbContext.SaveChangesAsync().ConfigureAwait(false);
                return addResult.Entity;
            }
            catch (Exception e)
            {
                _logger.LogError("Exception occured while saving worklog", e);
                throw;
            }
        }

        #endregion

        #region User Manipulaiton

        public async Task DeleteUser(int id)
        {
            try
            {
                var user = await _dbContext.UserSet.FindAsync(id);
                user.IsActive = false;
                await _dbContext.SaveChangesAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError("Exception occured while marking user as deleted", e);
                throw;
            }
        }

        public async Task<MirzaUser> GetUser(int id)
        {
            return await _dbContext.UserSet.FindAsync(id);
        }

        public async Task<MirzaUser> Register(MirzaUser user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            var validationResult = _userValidator.Validate(user);
            if (!validationResult.IsValid)
            {
                throw new UserModelValidationException(validationResult.Errors.Select(e => e.ErrorMessage).ToArray());
            }

            var duplicateEmail = await _dbContext.UserSet
                                                 .AnyAsync(u => u.IsActive && u.Email == user.Email)
                                                 .ConfigureAwait(false);

            if (duplicateEmail)
            {
                throw new DuplicateEmailException(user.Email);
            }

            try
            {
                await _dbContext.UserSet.AddAsync(user);
                await _dbContext.SaveChangesAsync().ConfigureAwait(true);
                return user;
            }
            catch (Exception e)
            {
                _logger.LogError("Exception occured while saving user entity", e);
                throw;
            }
        }

        #endregion
    }
}