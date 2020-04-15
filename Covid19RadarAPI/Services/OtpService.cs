﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using Covid19Radar.DataStore;
using Covid19Radar.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;

namespace Covid19Radar.Services
{
    public class OtpService : IOtpService
    {
        private readonly ICosmos _db;
        private readonly IOtpGenerator _otpGenerator;
        private readonly ISmsSender _smsSender;

        public OtpService(ICosmos db, IOtpGenerator otpGenerator, ISmsSender smsSender)
        {
            _db = db;
            _otpGenerator = otpGenerator;
            _smsSender = smsSender;
        }
        public async Task SendAsync(OtpSendRequest request)
        {
            //validate user existence
            var userExists = await UserExists(request.User);
            if (!userExists)
            {
                throw new UnauthorizedAccessException("Unauthorized access.");
            }

            //Generate otp
            var otpGeneratedTime = DateTime.UtcNow;
            var otp = _otpGenerator.Generate(otpGeneratedTime);
            
            //store otp generation
            await CreateOtpDocument(request.User, otpGeneratedTime);
            //send sms
            var sent = await _smsSender.SendAsync($"{otp} is your OTP for Covid19Radar. Valid for next 30 seconds.", request.Phone);
        }

        private async Task CreateOtpDocument(UserModel user, DateTime otpGeneratedTime)
        {
            var otpDocument = new OtpDocument()
            {
                id = $"{Guid.NewGuid():N}",
                UserUuid = user.UserUuid,
                UserId = user.GetId(),
                OtpCreatedTime = otpGeneratedTime
            };
            await _db.Otp.CreateItemAsync(otpDocument,new PartitionKey(user.UserUuid));
        }

        private async Task<bool> UserExists(UserModel user)
        {
            bool userFound = false;
            try
            {
                var userResult = await _db.User.ReadItemAsync<UserResultModel>(user.GetId(), PartitionKey.None);
                if (userResult.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    userFound = true;
                }
            }
            catch (CosmosException cosmosException)
            {
                if (cosmosException.StatusCode == HttpStatusCode.NotFound)
                {
                    userFound = false;
                }
            }

            return userFound;
        }
    }
}
